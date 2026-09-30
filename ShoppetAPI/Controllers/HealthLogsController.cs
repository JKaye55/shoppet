using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ShoppetAPI.Controllers;

[Route("api/pets/{petId:int}/[controller]")]
[ApiController]
public class HealthLogsController : ControllerBase
{
    private readonly IConfiguration _configuration;
    public HealthLogsController(IConfiguration configuration) => _configuration = configuration;

    private string ConnectionString =>
        _configuration.GetConnectionString("SharedSqlServer")
        ?? throw new InvalidOperationException("SharedSqlServer connection is missing.");

    [HttpGet]
    public async Task<IActionResult> GetHealthLogs(int petId)
    {
        try
        {
            var result = new List<object>();
            await using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync();

            const string sql = """
                SELECT Id, PetId, RecordType, Title, ISNULL(Notes,''), RecordDate,
                       NextDueDate, ISNULL(VetName,''),
                       ISNULL(Completed,0), DateAdministered,
                       ISNULL(ValidityInterval,0), ISNULL(ValidityUnit,'Months'),
                       ISNULL(MedicationIntervalHours,0), TimeStarted,
                       ISNULL(DosageTotal,0), ISNULL(DosageRemaining,0),
                       CheckupDate, ISNULL(DocumentPaths,''),
                       CompletedAt
                FROM PetHealthRecords
                WHERE PetId=@PetId
                ORDER BY ISNULL(NextDueDate, RecordDate) DESC, Id DESC;
                """;

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@PetId", petId);
            await using var r = await cmd.ExecuteReaderAsync();

            while (await r.ReadAsync())
            {
                string Iso(int ordinal) => r.IsDBNull(ordinal)
                    ? string.Empty
                    : r.GetDateTime(ordinal).ToString("O");

                result.Add(new
                {
                    Id = r.GetInt32(0),
                    PetId = r.GetInt32(1),
                    Type = NormalizeRecordType(r.IsDBNull(2) ? string.Empty : r.GetString(2)),
                    Name = r.IsDBNull(3) ? string.Empty : r.GetString(3),
                    Notes = r.GetString(4),
                    RecordDate = Iso(5),
                    DueDate = Iso(6),
                    VetName = r.GetString(7),
                    Completed = r.GetBoolean(8),
                    DateAdministered = !r.IsDBNull(9) ? Iso(9) : Iso(5),
                    ValidityInterval = r.GetInt32(10),
                    ValidityUnit = r.GetString(11),
                    MedicationIntervalHours = Convert.ToDouble(r.GetDecimal(12)),
                    TimeStarted = !r.IsDBNull(13) ? Iso(13) : Iso(5),
                    DosageTotal = r.GetInt32(14),
                    DosageRemaining = r.GetInt32(15),
                    CheckupDate = !r.IsDBNull(16) ? Iso(16) : Iso(5),
                    DocumentPaths = r.GetString(17),
                    CompletedAt = r.IsDBNull(18) ? (DateTime?)null : r.GetDateTime(18)
                });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Health records error: {ex.Message}");
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateHealthLog(int petId, [FromBody] HealthLogRequest request)
    {
        try
        {
            await using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync();

            const string sql = """
                INSERT INTO PetHealthRecords
                (PetId, RecordType, Title, Notes, RecordDate, NextDueDate, VetName,
                 Completed, DateAdministered, ValidityInterval, ValidityUnit,
                 MedicationIntervalHours, TimeStarted, DosageTotal, DosageRemaining,
                 CheckupDate, DocumentPaths, CompletedAt)
                OUTPUT INSERTED.Id
                VALUES
                (@PetId,@Type,@Name,'',SYSDATETIME(),@DueDate,'',
                 @Completed,@DateAdministered,@ValidityInterval,@ValidityUnit,
                 @MedicationIntervalHours,@TimeStarted,@DosageTotal,@DosageRemaining,
                 @CheckupDate,@DocumentPaths,@CompletedAt);
                """;

            await using var cmd = BuildCommand(sql, conn, petId, request);
            var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(new {
                Id=id, PetId=petId, request.Type, request.Name, request.DueDate,
                request.Completed, request.DateAdministered, request.ValidityInterval,
                request.ValidityUnit, request.MedicationIntervalHours, request.TimeStarted,
                request.DosageTotal, request.DosageRemaining, request.CheckupDate,
                request.DocumentPaths, CompletedAt=request.Completed ? DateTime.Now : (DateTime?)null
            });
        }
        catch (Exception ex) { return StatusCode(500, $"Health record save error: {ex.Message}"); }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateHealthLog(int petId, int id, [FromBody] HealthLogRequest request)
    {
        try
        {
            await using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            const string sql = """
                UPDATE PetHealthRecords SET
                    RecordType=@Type, Title=@Name, NextDueDate=@DueDate,
                    Completed=@Completed, DateAdministered=@DateAdministered,
                    ValidityInterval=@ValidityInterval, ValidityUnit=@ValidityUnit,
                    MedicationIntervalHours=@MedicationIntervalHours, TimeStarted=@TimeStarted,
                    DosageTotal=@DosageTotal, DosageRemaining=@DosageRemaining,
                    CheckupDate=@CheckupDate, DocumentPaths=@DocumentPaths,
                    CompletedAt=CASE WHEN @Completed=1 THEN ISNULL(CompletedAt,SYSDATETIME()) ELSE NULL END
                WHERE Id=@Id AND PetId=@PetId;
                """;
            await using var cmd = BuildCommand(sql, conn, petId, request);
            cmd.Parameters.AddWithValue("@Id", id);
            var rows = await cmd.ExecuteNonQueryAsync();
            return rows == 0 ? NotFound() : Ok(new { success=true });
        }
        catch (Exception ex) { return StatusCode(500, $"Health record update error: {ex.Message}"); }
    }

    [HttpPut("{id:int}/complete")]
    public async Task<IActionResult> CompleteHealthLog(int petId, int id, [FromBody] CompleteHealthRequest request)
    {
        try
        {
            await using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            const string sql = """
                UPDATE PetHealthRecords
                SET Completed=1, CompletedAt=SYSDATETIME(),
                    NextDueDate=COALESCE(@NextDueDate,NextDueDate)
                WHERE Id=@Id AND PetId=@PetId;
                """;
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@PetId", petId);
            cmd.Parameters.AddWithValue("@NextDueDate",
                ParseDate(request.NextDueDate) is DateTime d ? d : DBNull.Value);
            var rows = await cmd.ExecuteNonQueryAsync();
            return rows == 0 ? NotFound() : Ok(new { success=true });
        }
        catch (Exception ex) { return StatusCode(500, $"Complete health record error: {ex.Message}"); }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteHealthLog(int petId, int id)
    {
        try
        {
            await using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "DELETE FROM PetHealthRecords WHERE Id=@Id AND PetId=@PetId", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@PetId", petId);
            return await cmd.ExecuteNonQueryAsync() == 0 ? NotFound() : Ok(new { success=true });
        }
        catch (Exception ex) { return StatusCode(500, $"Delete health record error: {ex.Message}"); }
    }

    private static SqlCommand BuildCommand(string sql, SqlConnection conn, int petId, HealthLogRequest x)
    {
        var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@PetId", petId);
        cmd.Parameters.AddWithValue("@Type", x.Type ?? "vital");
        cmd.Parameters.AddWithValue("@Name", x.Name ?? string.Empty);
        cmd.Parameters.AddWithValue("@DueDate", ParseDate(x.DueDate) is DateTime due ? due : DBNull.Value);
        cmd.Parameters.AddWithValue("@Completed", x.Completed);
        cmd.Parameters.AddWithValue("@DateAdministered", ParseDate(x.DateAdministered) is DateTime da ? da : DBNull.Value);
        cmd.Parameters.AddWithValue("@ValidityInterval", x.ValidityInterval);
        cmd.Parameters.AddWithValue("@ValidityUnit", x.ValidityUnit ?? "Months");
        cmd.Parameters.AddWithValue("@MedicationIntervalHours", Convert.ToDecimal(x.MedicationIntervalHours));
        cmd.Parameters.AddWithValue("@TimeStarted", ParseDate(x.TimeStarted) is DateTime ts ? ts : DBNull.Value);
        cmd.Parameters.AddWithValue("@DosageTotal", x.DosageTotal);
        cmd.Parameters.AddWithValue("@DosageRemaining", x.DosageRemaining);
        cmd.Parameters.AddWithValue("@CheckupDate", ParseDate(x.CheckupDate) is DateTime cd ? cd : DBNull.Value);
        cmd.Parameters.AddWithValue("@DocumentPaths", x.DocumentPaths ?? string.Empty);
        cmd.Parameters.AddWithValue("@CompletedAt", x.Completed ? DateTime.Now : DBNull.Value);
        return cmd;
    }

    private static DateTime? ParseDate(string? value) =>
        DateTime.TryParse(value, out var dt) ? dt : null;

    private static string NormalizeRecordType(string? value)
    {
        var v = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (v.Contains("vacc")) return "vaccine";
        if (v.Contains("med")) return "medication";
        return "vital";
    }
}

public class HealthLogRequest
{
    public string Type { get; set; } = "vital";
    public string Name { get; set; } = string.Empty;
    public string DueDate { get; set; } = string.Empty;
    public bool Completed { get; set; }
    public string DateAdministered { get; set; } = string.Empty;
    public int ValidityInterval { get; set; }
    public string ValidityUnit { get; set; } = "Months";
    public double MedicationIntervalHours { get; set; }
    public string TimeStarted { get; set; } = string.Empty;
    public int DosageTotal { get; set; }
    public int DosageRemaining { get; set; }
    public string CheckupDate { get; set; } = string.Empty;
    public string DocumentPaths { get; set; } = string.Empty;
}
public class CompleteHealthRequest { public string NextDueDate { get; set; } = string.Empty; }
