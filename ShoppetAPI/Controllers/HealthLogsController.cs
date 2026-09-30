using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ShoppetAPI.Controllers;

[Route("api/pets/{petId}/[controller]")]
[ApiController]
public class HealthLogsController : ControllerBase
{
    private readonly IConfiguration _configuration;
    public HealthLogsController(IConfiguration configuration) => _configuration = configuration;

    private string ConnectionString =>
        _configuration.GetConnectionString("SharedSqlServer")
        ?? throw new InvalidOperationException("SharedSqlServer connection string not found.");

    [HttpGet]
    public async Task<IActionResult> GetHealthLogs(int petId)
    {
        try
        {
            var logs = new List<object>();
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            await using var cmd = new SqlCommand(@"
                SELECT Id, PetId, RecordType, Title, DueDateText, Completed,
                       DateAdministeredText, ValidityInterval, ValidityUnit,
                       MedicationIntervalHours, TimeStarted, DosageTotal, DosageRemaining,
                       CheckupDateText, DocumentPaths, CreatedAt, CompletedAt
                FROM PetHealthRecords
                WHERE PetId=@PetId
                ORDER BY CreatedAt DESC;", connection);
            cmd.Parameters.AddWithValue("@PetId", petId);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                logs.Add(new
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    PetId = reader.GetInt32(reader.GetOrdinal("PetId")),
                    Type = reader.GetString(reader.GetOrdinal("RecordType")),
                    Name = reader.GetString(reader.GetOrdinal("Title")),
                    DueDate = reader.IsDBNull(reader.GetOrdinal("DueDateText")) ? "" : reader.GetString(reader.GetOrdinal("DueDateText")),
                    Completed = reader.GetBoolean(reader.GetOrdinal("Completed")),
                    DateAdministered = reader.IsDBNull(reader.GetOrdinal("DateAdministeredText")) ? "" : reader.GetString(reader.GetOrdinal("DateAdministeredText")),
                    ValidityInterval = reader.GetInt32(reader.GetOrdinal("ValidityInterval")),
                    ValidityUnit = reader.IsDBNull(reader.GetOrdinal("ValidityUnit")) ? "" : reader.GetString(reader.GetOrdinal("ValidityUnit")),
                    MedicationIntervalHours = reader.GetDouble(reader.GetOrdinal("MedicationIntervalHours")),
                    TimeStarted = reader.IsDBNull(reader.GetOrdinal("TimeStarted")) ? "" : reader.GetString(reader.GetOrdinal("TimeStarted")),
                    DosageTotal = reader.GetInt32(reader.GetOrdinal("DosageTotal")),
                    DosageRemaining = reader.GetInt32(reader.GetOrdinal("DosageRemaining")),
                    CheckupDate = reader.IsDBNull(reader.GetOrdinal("CheckupDateText")) ? "" : reader.GetString(reader.GetOrdinal("CheckupDateText")),
                    DocumentPaths = reader.IsDBNull(reader.GetOrdinal("DocumentPaths")) ? "" : reader.GetString(reader.GetOrdinal("DocumentPaths")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                    CompletedAt = reader.IsDBNull(reader.GetOrdinal("CompletedAt")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("CompletedAt"))
                });
            }

            return Ok(logs);
        }
        catch (Exception ex) { return StatusCode(500, $"Error fetching health logs: {ex.Message}"); }
    }

    [HttpPost]
    public async Task<IActionResult> CreateHealthLog(int petId, [FromBody] HealthLogRequest request)
    {
        if (petId <= 0) return BadRequest("A valid pet ID is required.");

        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            await using var cmd = new SqlCommand(@"
                INSERT INTO PetHealthRecords
                    (PetId, ClinicId, RecordType, Title, Notes, RecordDate, NextDueDate, VetName, CreatedAt,
                     DueDateText, Completed, DateAdministeredText, ValidityInterval, ValidityUnit,
                     MedicationIntervalHours, TimeStarted, DosageTotal, DosageRemaining,
                     CheckupDateText, DocumentPaths, CompletedAt)
                OUTPUT INSERTED.Id
                VALUES
                    (@PetId, NULL, @Type, @Name, '', SYSDATETIME(), NULL, '', SYSDATETIME(),
                     @DueDate, @Completed, @DateAdministered, @ValidityInterval, @ValidityUnit,
                     @MedicationIntervalHours, @TimeStarted, @DosageTotal, @DosageRemaining,
                     @CheckupDate, @DocumentPaths,
                     CASE WHEN @Completed=1 THEN SYSDATETIME() ELSE NULL END);", connection);

            AddParameters(cmd, petId, request);
            int id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(new { Id = id });
        }
        catch (Exception ex) { return StatusCode(500, $"Error saving health log: {ex.Message}"); }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateHealthLog(int petId, int id, [FromBody] HealthLogRequest request)
    {
        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            await using var cmd = new SqlCommand(@"
                UPDATE PetHealthRecords
                SET RecordType=@Type,
                    Title=@Name,
                    DueDateText=@DueDate,
                    Completed=@Completed,
                    DateAdministeredText=@DateAdministered,
                    ValidityInterval=@ValidityInterval,
                    ValidityUnit=@ValidityUnit,
                    MedicationIntervalHours=@MedicationIntervalHours,
                    TimeStarted=@TimeStarted,
                    DosageTotal=@DosageTotal,
                    DosageRemaining=@DosageRemaining,
                    CheckupDateText=@CheckupDate,
                    DocumentPaths=@DocumentPaths,
                    CompletedAt=CASE
                        WHEN @Completed=1 AND CompletedAt IS NULL THEN SYSDATETIME()
                        WHEN @Completed=0 THEN NULL
                        ELSE CompletedAt
                    END
                WHERE Id=@Id AND PetId=@PetId;", connection);

            cmd.Parameters.AddWithValue("@Id", id);
            AddParameters(cmd, petId, request);

            if (await cmd.ExecuteNonQueryAsync() == 0) return NotFound("Health record not found.");
            return Ok();
        }
        catch (Exception ex) { return StatusCode(500, $"Error updating health log: {ex.Message}"); }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteHealthLog(int petId, int id)
    {
        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var cmd = new SqlCommand("DELETE FROM PetHealthRecords WHERE Id=@Id AND PetId=@PetId", connection);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@PetId", petId);
            return await cmd.ExecuteNonQueryAsync() > 0 ? Ok() : NotFound("Health record not found.");
        }
        catch (Exception ex) { return StatusCode(500, $"Error deleting health log: {ex.Message}"); }
    }

    [HttpPut("{id:int}/complete")]
    public async Task<IActionResult> CompleteHealthLog(int petId, int id, [FromBody] CompleteHealthLogRequest? request)
    {
        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var cmd = new SqlCommand(@"
                UPDATE PetHealthRecords
                SET Completed=1,
                    CompletedAt=SYSDATETIME(),
                    DosageRemaining=0,
                    DueDateText=CASE WHEN @NextDueDate='' THEN DueDateText ELSE @NextDueDate END
                WHERE Id=@Id AND PetId=@PetId;", connection);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@PetId", petId);
            cmd.Parameters.AddWithValue("@NextDueDate", request?.NextDueDate ?? "");
            return await cmd.ExecuteNonQueryAsync() > 0 ? Ok() : NotFound("Health record not found.");
        }
        catch (Exception ex) { return StatusCode(500, $"Error completing health log: {ex.Message}"); }
    }

    private static void AddParameters(SqlCommand cmd, int petId, HealthLogRequest request)
    {
        cmd.Parameters.AddWithValue("@PetId", petId);
        cmd.Parameters.AddWithValue("@Type", request.Type ?? "vaccine");
        cmd.Parameters.AddWithValue("@Name", request.Name ?? "");
        cmd.Parameters.AddWithValue("@DueDate", request.DueDate ?? "");
        cmd.Parameters.AddWithValue("@Completed", request.Completed);
        cmd.Parameters.AddWithValue("@DateAdministered", request.DateAdministered ?? "");
        cmd.Parameters.AddWithValue("@ValidityInterval", request.ValidityInterval);
        cmd.Parameters.AddWithValue("@ValidityUnit", request.ValidityUnit ?? "Months");
        cmd.Parameters.AddWithValue("@MedicationIntervalHours", request.MedicationIntervalHours);
        cmd.Parameters.AddWithValue("@TimeStarted", request.TimeStarted ?? "");
        cmd.Parameters.AddWithValue("@DosageTotal", request.DosageTotal);
        cmd.Parameters.AddWithValue("@DosageRemaining", request.DosageRemaining);
        cmd.Parameters.AddWithValue("@CheckupDate", request.CheckupDate ?? "");
        cmd.Parameters.AddWithValue("@DocumentPaths", request.DocumentPaths ?? "");
    }
}

public class HealthLogRequest
{
    public string Type { get; set; } = "vaccine";
    public string Name { get; set; } = "";
    public string DueDate { get; set; } = "";
    public bool Completed { get; set; }
    public string DateAdministered { get; set; } = "";
    public int ValidityInterval { get; set; }
    public string ValidityUnit { get; set; } = "Months";
    public double MedicationIntervalHours { get; set; }
    public string TimeStarted { get; set; } = "";
    public int DosageTotal { get; set; }
    public int DosageRemaining { get; set; }
    public string CheckupDate { get; set; } = "";
    public string DocumentPaths { get; set; } = "";
}

public class CompleteHealthLogRequest
{
    public string NextDueDate { get; set; } = "";
}
