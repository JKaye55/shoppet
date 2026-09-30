using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ShoppetAPI.Controllers;

[Route("api/pets/{petId:int}/[controller]")]
[ApiController]
public class FoodLogsController : ControllerBase
{
    private readonly IConfiguration _configuration;
    public FoodLogsController(IConfiguration configuration) => _configuration = configuration;

    private string ConnectionString =>
        _configuration.GetConnectionString("SharedSqlServer")
        ?? throw new InvalidOperationException("SharedSqlServer connection is missing.");

    [HttpGet]
    public async Task<IActionResult> GetFoodLogs(int petId)
    {
        try
        {
            var result = new List<object>();
            await using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            const string sql = """
                SELECT Id, PetId, FoodName, ISNULL(AmountGrams,0),
                       ISNULL(IntervalHours,0), ISNULL(IntervalMinutes,0),
                       StartTimestamp, LastFedTimestamp, FedDate,
                       ISNULL(Notes,''), ISNULL(IsCompleted,0), CompletedAt
                FROM FoodLogs
                WHERE PetId=@PetId
                ORDER BY ISNULL(LastFedTimestamp, ISNULL(FedAt,CreatedAt)) DESC, Id DESC;
                """;
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@PetId", petId);
            await using var r = await cmd.ExecuteReaderAsync();

            while (await r.ReadAsync())
            {
                string Iso(int i) => r.IsDBNull(i) ? string.Empty : r.GetDateTime(i).ToString("O");
                result.Add(new {
                    Id=r.GetInt32(0), PetId=r.GetInt32(1), FoodName=r.GetString(2),
                    AmountGrams=r.GetDouble(3), IntervalHours=r.GetInt32(4),
                    IntervalMinutes=r.GetInt32(5), StartTimestamp=Iso(6),
                    LastFedTimestamp=Iso(7), FedDate=Iso(8), Notes=r.GetString(9),
                    IsCompleted=r.GetBoolean(10),
                    CompletedAt=r.IsDBNull(11)?(DateTime?)null:r.GetDateTime(11)
                });
            }
            return Ok(result);
        }
        catch(Exception ex){ return StatusCode(500,$"Food logs error: {ex.Message}"); }
    }

    [HttpPost]
    public async Task<IActionResult> CreateFoodLog(int petId,[FromBody] FoodLogRequest request)
    {
        try
        {
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            const string sql="""
                INSERT INTO FoodLogs
                (PetId,FoodName,PortionSize,Notes,FedAt,CreatedAt,AmountGrams,
                 IntervalHours,IntervalMinutes,StartTimestamp,LastFedTimestamp,
                 FedDate,IsCompleted,CompletedAt)
                OUTPUT INSERTED.Id
                VALUES
                (@PetId,@FoodName,@PortionSize,@Notes,
                 COALESCE(@LastFed,SYSDATETIME()),SYSDATETIME(),@Amount,
                 @IH,@IM,@Start,@LastFed,@FedDate,0,NULL);
                """;
            await using var cmd=Build(sql,conn,petId,request);
            var id=Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(new {
                Id=id,PetId=petId,request.FoodName,request.AmountGrams,
                request.IntervalHours,request.IntervalMinutes,request.StartTimestamp,
                LastFedTimestamp="",FedDate="",request.Notes,IsCompleted=false,
                CompletedAt=(DateTime?)null
            });
        }
        catch(Exception ex){ return StatusCode(500,$"Food log save error: {ex.Message}"); }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateFoodLog(int petId,int id,[FromBody] FoodLogRequest request)
    {
        try
        {
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            const string sql="""
                UPDATE FoodLogs SET
                    FoodName=@FoodName, PortionSize=@PortionSize, Notes=@Notes,
                    AmountGrams=@Amount, IntervalHours=@IH, IntervalMinutes=@IM,
                    StartTimestamp=@Start
                WHERE Id=@Id AND PetId=@PetId;
                """;
            await using var cmd=Build(sql,conn,petId,request);
            cmd.Parameters.AddWithValue("@Id",id);
            return await cmd.ExecuteNonQueryAsync()==0?NotFound():Ok(new{success=true});
        }
        catch(Exception ex){ return StatusCode(500,$"Food log update error: {ex.Message}"); }
    }

    [HttpPost("{id:int}/done")]
    public async Task<IActionResult> MarkFed(int petId,int id)
    {
        try
        {
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            const string sql="""
                UPDATE FoodLogs
                SET LastFedTimestamp=SYSDATETIME(), FedDate=SYSDATETIME(),
                    FedAt=SYSDATETIME()
                WHERE Id=@Id AND PetId=@PetId;

                SELECT Id,PetId,FoodName,ISNULL(AmountGrams,0),ISNULL(IntervalHours,0),
                       ISNULL(IntervalMinutes,0),StartTimestamp,LastFedTimestamp,FedDate,
                       ISNULL(Notes,''),ISNULL(IsCompleted,0),CompletedAt
                FROM FoodLogs WHERE Id=@Id AND PetId=@PetId;
                """;
            await using var cmd=new SqlCommand(sql,conn);
            cmd.Parameters.AddWithValue("@Id",id);
            cmd.Parameters.AddWithValue("@PetId",petId);
            await using var r=await cmd.ExecuteReaderAsync();
            if(!await r.ReadAsync()) return NotFound();
            string Iso(int i)=>r.IsDBNull(i)?string.Empty:r.GetDateTime(i).ToString("O");
            return Ok(new {
                Id=r.GetInt32(0),PetId=r.GetInt32(1),FoodName=r.GetString(2),
                AmountGrams=r.GetDouble(3),IntervalHours=r.GetInt32(4),
                IntervalMinutes=r.GetInt32(5),StartTimestamp=Iso(6),
                LastFedTimestamp=Iso(7),FedDate=Iso(8),Notes=r.GetString(9),
                IsCompleted=r.GetBoolean(10),
                CompletedAt=r.IsDBNull(11)?(DateTime?)null:r.GetDateTime(11)
            });
        }
        catch(Exception ex){ return StatusCode(500,$"Mark fed error: {ex.Message}"); }
    }

    [HttpPut("{id:int}/complete")]
    public async Task<IActionResult> CompleteFoodLog(int petId,int id)
    {
        try
        {
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            await using var cmd=new SqlCommand("""
                UPDATE FoodLogs
                SET IsCompleted=1,CompletedAt=SYSDATETIME()
                WHERE Id=@Id AND PetId=@PetId;
                """,conn);
            cmd.Parameters.AddWithValue("@Id",id);
            cmd.Parameters.AddWithValue("@PetId",petId);
            return await cmd.ExecuteNonQueryAsync()==0?NotFound():Ok(new{success=true});
        }
        catch(Exception ex){ return StatusCode(500,$"Complete food log error: {ex.Message}"); }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteFoodLog(int petId,int id)
    {
        try
        {
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            await using var cmd=new SqlCommand("DELETE FROM FoodLogs WHERE Id=@Id AND PetId=@PetId",conn);
            cmd.Parameters.AddWithValue("@Id",id);
            cmd.Parameters.AddWithValue("@PetId",petId);
            return await cmd.ExecuteNonQueryAsync()==0?NotFound():Ok(new{success=true});
        }
        catch(Exception ex){ return StatusCode(500,$"Delete food log error: {ex.Message}"); }
    }

    private static SqlCommand Build(string sql,SqlConnection conn,int petId,FoodLogRequest x)
    {
        var cmd=new SqlCommand(sql,conn);
        cmd.Parameters.AddWithValue("@PetId",petId);
        cmd.Parameters.AddWithValue("@FoodName",x.FoodName??string.Empty);
        cmd.Parameters.AddWithValue("@PortionSize",x.AmountGrams>0?$"{x.AmountGrams:0.##} g":string.Empty);
        cmd.Parameters.AddWithValue("@Notes",x.Notes??string.Empty);
        cmd.Parameters.AddWithValue("@Amount",x.AmountGrams);
        cmd.Parameters.AddWithValue("@IH",x.IntervalHours);
        cmd.Parameters.AddWithValue("@IM",x.IntervalMinutes);
        cmd.Parameters.AddWithValue("@Start",Parse(x.StartTimestamp) is DateTime s?s:DBNull.Value);
        cmd.Parameters.AddWithValue("@LastFed",DBNull.Value);
        cmd.Parameters.AddWithValue("@FedDate",DBNull.Value);
        return cmd;
    }

    private static DateTime? Parse(string? value)=>DateTime.TryParse(value,out var d)?d:null;
}

public class FoodLogRequest
{
    public string FoodName{get;set;}=string.Empty;
    public double AmountGrams{get;set;}
    public int IntervalHours{get;set;}
    public int IntervalMinutes{get;set;}
    public string StartTimestamp{get;set;}=string.Empty;
    public string Notes{get;set;}=string.Empty;
}
