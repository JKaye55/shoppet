using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ShoppetAPI.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IConfiguration _configuration;
    public PaymentsController(IConfiguration configuration)=>_configuration=configuration;

    private string ConnectionString =>
        _configuration.GetConnectionString("SharedSqlServer")
        ?? throw new InvalidOperationException("SharedSqlServer connection string not found.");

    [HttpPost("mock")]
    public async Task<IActionResult> MockPayment([FromBody]MockPaymentRequest request)
    {
        if(request.UserId<=0) return BadRequest("A valid user ID is required.");
        if(request.Amount<0) return BadRequest("Amount cannot be negative.");

        string method=string.IsNullOrWhiteSpace(request.PaymentMethod)
            ?"GCash Mock"
            :request.PaymentMethod.Trim();

        string status=request.SimulateSuccess ? "SimulatedPaid" : "SimulatedFailed";
        string reference=$"MOCK-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

        try
        {
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();

            await using var cmd=new SqlCommand(@"
                INSERT INTO Transactions
                    (UserId,ClinicId,Type,Amount,Reference,PaidAt,PaymentMethod,Status,RelatedListingId,IsSimulation)
                OUTPUT INSERTED.Id
                VALUES
                    (@UserId,NULL,@Type,@Amount,@Reference,SYSDATETIME(),@PaymentMethod,@Status,@ListingId,1);",conn);

            cmd.Parameters.AddWithValue("@UserId",request.UserId);
            cmd.Parameters.AddWithValue("@Type",string.IsNullOrWhiteSpace(request.Type)?"MarketplacePurchase":request.Type.Trim());
            cmd.Parameters.AddWithValue("@Amount",request.Amount);
            cmd.Parameters.AddWithValue("@Reference",reference);
            cmd.Parameters.AddWithValue("@PaymentMethod",method);
            cmd.Parameters.AddWithValue("@Status",status);
            cmd.Parameters.AddWithValue("@ListingId",(object?)request.ListingId ?? DBNull.Value);

            int id=Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(new{
                Id=id,
                request.UserId,
                request.ListingId,
                request.Amount,
                PaymentMethod=method,
                Status=status,
                Reference=reference,
                IsSimulation=true,
                Message=request.SimulateSuccess
                    ?"Payment simulation successful. No real money was processed."
                    :"Payment simulation failed. No real money was processed."
            });
        }
        catch(Exception ex){ return StatusCode(500,$"Mock payment error: {ex.Message}"); }
    }
}

public class MockPaymentRequest
{
    public int UserId{get;set;}
    public int? ListingId{get;set;}
    public decimal Amount{get;set;}
    public string PaymentMethod{get;set;}="GCash Mock";
    public string Type{get;set;}="MarketplacePurchase";
    public bool SimulateSuccess{get;set;}=true;
}
