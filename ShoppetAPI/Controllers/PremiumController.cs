using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ShoppetAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PremiumController : ControllerBase
{
    private readonly IConfiguration _configuration;
    public PremiumController(IConfiguration configuration) => _configuration = configuration;

    private string ConnectionString =>
        _configuration.GetConnectionString("SharedSqlServer")
        ?? throw new InvalidOperationException("SharedSqlServer connection is missing.");

    [HttpGet("status")]
    public async Task<IActionResult> Status([FromQuery] int userId)
    {
        await using var conn=new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd=new SqlCommand("""
            SELECT ISNULL(IsPremium,0),PremiumActivatedAt,ISNULL(PremiumReference,'')
            FROM UserAccounts WITH(UPDLOCK,HOLDLOCK) WHERE Id=@UserId;
            """,conn);
        cmd.Parameters.AddWithValue("@UserId",userId);
        await using var r=await cmd.ExecuteReaderAsync();
        if(!await r.ReadAsync()) return NotFound();
        return Ok(new{
            IsPremium=r.GetBoolean(0),
            ActivatedAt=r.IsDBNull(1)?(DateTime?)null:r.GetDateTime(1),
            Reference=r.GetString(2),
            Price=150m
        });
    }

    [HttpPost("activate")]
    public async Task<IActionResult> Activate([FromQuery] int userId,[FromQuery]bool simulateSuccess=true)
    {
        if(userId<=0) return BadRequest("A valid user is required.");
        if(!simulateSuccess)return BadRequest("Simulated payment failed. Premium status is unchanged.");
        await using var conn=new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var tx=(SqlTransaction)await conn.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

        await using(var check=new SqlCommand(
            "SELECT ISNULL(IsPremium,0) FROM UserAccounts WITH(UPDLOCK,HOLDLOCK) WHERE Id=@UserId",conn,tx))
        {
            check.Parameters.AddWithValue("@UserId",userId);
            var existing=await check.ExecuteScalarAsync();
            if(existing is null) return NotFound();
            if(Convert.ToBoolean(existing))
                return Ok(new{success=true,alreadyPremium=true});
        }

        var reference="PREM-"+Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();

        await using(var update=new SqlCommand("""
            UPDATE UserAccounts
            SET IsPremium=1,PremiumActivatedAt=SYSDATETIME(),PremiumReference=@Reference
            WHERE Id=@UserId;
            """,conn,tx))
        {
            update.Parameters.AddWithValue("@UserId",userId);
            update.Parameters.AddWithValue("@Reference",reference);
            await update.ExecuteNonQueryAsync();
        }

        await using(var log=new SqlCommand("""
            INSERT INTO Transactions
                (UserId,Type,Amount,Reference,PaidAt,PaymentMethod,Status)
            VALUES
                (@UserId,'PremiumUpgrade',150,@Reference,SYSDATETIME(),'Mock Payment','SimulatedPaid');
            """,conn,tx))
        {
            log.Parameters.AddWithValue("@UserId",userId);
            log.Parameters.AddWithValue("@Reference",reference);
            await log.ExecuteNonQueryAsync();
        }

        await using(var notify=new SqlCommand("""
            INSERT INTO Notifications(UserId,Title,Body,Link,Icon,IsRead,CreatedAt)
            VALUES(@UserId,'Premium activated',
                   'Your ShoppetCare Premium (₱150 / 2 Months) upgrade is active.',
                   'premium','',0,SYSDATETIME());
            """,conn,tx))
        {
            notify.Parameters.AddWithValue("@UserId",userId);
            await notify.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
        return Ok(new{success=true,alreadyPremium=false,reference});
    }
}
