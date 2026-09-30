using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ShoppetAPI.Controllers;

[Route("api/pets/{petId}/[controller]")]
[ApiController]
public class FoodLogsController : ControllerBase
{
    private readonly IConfiguration _configuration;
    public FoodLogsController(IConfiguration configuration) => _configuration = configuration;

    private string ConnectionString =>
        _configuration.GetConnectionString("SharedSqlServer")
        ?? throw new InvalidOperationException("SharedSqlServer connection string not found.");

    [HttpGet]
    public async Task<IActionResult> GetFoodLogs(int petId)
    {
        try
        {
            var logs = new List<object>();
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            await using var cmd = new SqlCommand(@"
                SELECT Id, PetId, FoodName, AmountGrams, IntervalHours, IntervalMinutes,
                       StartTimestamp, LastFedTimestamp, FedDate, Notes, CreatedAt,
                       IsCompleted, CompletedAt
                FROM FoodLogs
                WHERE PetId=@PetId
                ORDER BY CreatedAt DESC;", connection);
            cmd.Parameters.AddWithValue("@PetId", petId);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                logs.Add(new
                {
                    Id = reader.GetInt32(0),
                    PetId = reader.GetInt32(1),
                    FoodName = reader.GetString(2),
                    AmountGrams = reader.GetDouble(3),
                    IntervalHours = reader.GetInt32(4),
                    IntervalMinutes = reader.GetInt32(5),
                    StartTimestamp = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    LastFedTimestamp = reader.IsDBNull(7) ? "" : reader.GetString(7),
                    FedDate = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    Notes = reader.IsDBNull(9) ? "" : reader.GetString(9),
                    CreatedAt = reader.GetDateTime(10),
                    IsCompleted = reader.GetBoolean(11),
                    CompletedAt = reader.IsDBNull(12) ? (DateTime?)null : reader.GetDateTime(12)
                });
            }

            return Ok(logs);
        }
        catch (Exception ex) { return StatusCode(500, $"Error fetching food logs: {ex.Message}"); }
    }

    [HttpPost]
    public async Task<IActionResult> CreateFoodLog(int petId, [FromBody] FoodLogRequest request)
    {
        if (petId <= 0) return BadRequest("A valid pet ID is required.");

        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            await using var cmd = new SqlCommand(@"
                INSERT INTO FoodLogs
                    (PetId, FoodName, PortionSize, Notes, FedAt, CreatedAt,
                     AmountGrams, IntervalHours, IntervalMinutes, StartTimestamp,
                     LastFedTimestamp, FedDate, IsCompleted, CompletedAt)
                OUTPUT INSERTED.Id
                VALUES
                    (@PetId, @FoodName, @PortionSize, @Notes, SYSDATETIME(), SYSDATETIME(),
                     @AmountGrams, @IntervalHours, @IntervalMinutes, @StartTimestamp,
                     @LastFedTimestamp, @FedDate, 0, NULL);", connection);

            AddParameters(cmd, petId, request);
            int id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(new { Id = id });
        }
        catch (Exception ex) { return StatusCode(500, $"Error saving food log: {ex.Message}"); }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateFoodLog(int petId, int id, [FromBody] FoodLogRequest request)
    {
        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            await using var cmd = new SqlCommand(@"
                UPDATE FoodLogs
                SET FoodName=@FoodName,
                    PortionSize=@PortionSize,
                    AmountGrams=@AmountGrams,
                    IntervalHours=@IntervalHours,
                    IntervalMinutes=@IntervalMinutes,
                    StartTimestamp=@StartTimestamp,
                    LastFedTimestamp=@LastFedTimestamp,
                    FedDate=@FedDate,
                    Notes=@Notes
                WHERE Id=@Id AND PetId=@PetId;", connection);

            cmd.Parameters.AddWithValue("@Id", id);
            AddParameters(cmd, petId, request);
            return await cmd.ExecuteNonQueryAsync() > 0 ? Ok() : NotFound("Food log not found.");
        }
        catch (Exception ex) { return StatusCode(500, $"Error updating food log: {ex.Message}"); }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteFoodLog(int petId, int id)
    {
        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var cmd = new SqlCommand("DELETE FROM FoodLogs WHERE Id=@Id AND PetId=@PetId", connection);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@PetId", petId);
            return await cmd.ExecuteNonQueryAsync() > 0 ? Ok() : NotFound("Food log not found.");
        }
        catch (Exception ex) { return StatusCode(500, $"Error deleting food log: {ex.Message}"); }
    }

    [HttpPut("{id:int}/complete")]
    public async Task<IActionResult> CompleteFoodLog(int petId, int id)
    {
        try
        {
            string lastFed = DateTimeOffset.Now.ToString("o");
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            await using var cmd = new SqlCommand(@"
                UPDATE FoodLogs
                SET IsCompleted=1,
                    CompletedAt=SYSDATETIME(),
                    LastFedTimestamp=@LastFed,
                    FedAt=SYSDATETIME()
                WHERE Id=@Id AND PetId=@PetId;", connection);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@PetId", petId);
            cmd.Parameters.AddWithValue("@LastFed", lastFed);

            return await cmd.ExecuteNonQueryAsync() > 0
                ? Ok(new { LastFedTimestamp = lastFed })
                : NotFound("Food log not found.");
        }
        catch (Exception ex) { return StatusCode(500, $"Error completing food log: {ex.Message}"); }
    }

    private static void AddParameters(SqlCommand cmd, int petId, FoodLogRequest request)
    {
        cmd.Parameters.AddWithValue("@PetId", petId);
        cmd.Parameters.AddWithValue("@FoodName", request.FoodName ?? "");
        cmd.Parameters.AddWithValue("@PortionSize", request.AmountGrams > 0 ? $"{request.AmountGrams:0.##} g" : "");
        cmd.Parameters.AddWithValue("@AmountGrams", request.AmountGrams);
        cmd.Parameters.AddWithValue("@IntervalHours", request.IntervalHours);
        cmd.Parameters.AddWithValue("@IntervalMinutes", request.IntervalMinutes);
        cmd.Parameters.AddWithValue("@StartTimestamp", request.StartTimestamp ?? "");
        cmd.Parameters.AddWithValue("@LastFedTimestamp", request.LastFedTimestamp ?? "");
        cmd.Parameters.AddWithValue("@FedDate", request.FedDate ?? "");
        cmd.Parameters.AddWithValue("@Notes", request.Notes ?? "");
    }
}

public class FoodLogRequest
{
    public string FoodName { get; set; } = "";
    public double AmountGrams { get; set; }
    public int IntervalHours { get; set; }
    public int IntervalMinutes { get; set; }
    public string StartTimestamp { get; set; } = "";
    public string LastFedTimestamp { get; set; } = "";
    public string FedDate { get; set; } = "";
    public string Notes { get; set; } = "";
}
