using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Data;

namespace ShoppetAPI.Controllers
{
    [Route("api/pets/{petId}/[controller]")]
    [ApiController]
    public class HealthLogsController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public HealthLogsController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> GetHealthLogs(int petId)
        {
            try
            {
                string connString = _configuration.GetConnectionString("DefaultConnection")!;
                var logs = new List<object>();

                using (var connection = new MySqlConnection(connString))
                {
                    await connection.OpenAsync();
                    var query = "SELECT Id, PetId, Type, Name, DueDate, Completed, DateAdministered, ValidityInterval, ValidityUnit, MedicationIntervalHours, TimeStarted, DosageTotal, DosageRemaining, CheckupDate, DocumentPaths, CreatedAt, CompletedAt FROM healthlogs WHERE PetId = @PetId";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@PetId", petId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                logs.Add(new
                                {
                                    Id = reader.GetInt32("Id"),
                                    PetId = reader.GetInt32("PetId"),
                                    Type = reader.GetString("Type"),
                                    Name = reader.GetString("Name"),
                                    DueDate = reader.IsDBNull(reader.GetOrdinal("DueDate")) ? "" : reader.GetString("DueDate"),
                                    Completed = reader.GetBoolean("Completed"),
                                    DateAdministered = reader.IsDBNull(reader.GetOrdinal("DateAdministered")) ? "" : reader.GetString("DateAdministered"),
                                    ValidityInterval = reader.GetInt32("ValidityInterval"),
                                    ValidityUnit = reader.IsDBNull(reader.GetOrdinal("ValidityUnit")) ? "" : reader.GetString("ValidityUnit"),
                                    MedicationIntervalHours = reader.GetDouble("MedicationIntervalHours"),
                                    TimeStarted = reader.IsDBNull(reader.GetOrdinal("TimeStarted")) ? "" : reader.GetString("TimeStarted"),
                                    DosageTotal = reader.GetInt32("DosageTotal"),
                                    DosageRemaining = reader.GetInt32("DosageRemaining"),
                                    CheckupDate = reader.IsDBNull(reader.GetOrdinal("CheckupDate")) ? "" : reader.GetString("CheckupDate"),
                                    DocumentPaths = reader.IsDBNull(reader.GetOrdinal("DocumentPaths")) ? "" : reader.GetString("DocumentPaths"),
                                    CreatedAt = reader.GetDateTime("CreatedAt"),
                                    CompletedAt = reader.IsDBNull(reader.GetOrdinal("CompletedAt")) ? (DateTime?)null : reader.GetDateTime("CompletedAt")
                                });
                            }
                        }
                    }
                }

                return Ok(logs);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error fetching health logs: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateHealthLog(int petId, [FromBody] HealthLogRequest request)
        {
            try
            {
                if (petId <= 0)
                    return BadRequest("A valid pet ID is required.");

                string connString = _configuration.GetConnectionString("DefaultConnection")!;
                long newId = 0;

                using (var connection = new MySqlConnection(connString))
                {
                    await connection.OpenAsync();
                    var query = @"INSERT INTO healthlogs
                                  (PetId, Type, Name, DueDate, Completed, DateAdministered,
                                   ValidityInterval, ValidityUnit, MedicationIntervalHours,
                                   TimeStarted, DosageTotal, DosageRemaining, CheckupDate,
                                   DocumentPaths, CreatedAt, CompletedAt)
                                  VALUES
                                  (@PetId, @Type, @Name, @DueDate, @Completed, @DateAdministered,
                                   @ValidityInterval, @ValidityUnit, @MedicationIntervalHours,
                                   @TimeStarted, @DosageTotal, @DosageRemaining, @CheckupDate,
                                   @DocumentPaths, NOW(),
                                   CASE WHEN @Completed = 1 THEN NOW() ELSE NULL END)";

                    using (var cmd = new MySqlCommand(query, connection))
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
                        cmd.Parameters.AddWithValue("@Completed", request.Completed);

                        int rows = await cmd.ExecuteNonQueryAsync();
                        if (rows == 0)
                            return NotFound("Health record not found.");
                        newId = cmd.LastInsertedId;
                    }
                }

                return Ok(new { Id = (int)newId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error saving health log: {ex.Message}");
            }
        }

                [HttpPut("{id}")]
        public async Task<IActionResult> UpdateHealthLog(int petId, int id, [FromBody] HealthLogRequest request)
        {
            try
            {
                string connString = _configuration.GetConnectionString("DefaultConnection")!;
                using (var connection = new MySqlConnection(connString))
                {
                    await connection.OpenAsync();
                    var query = @"UPDATE healthlogs 
                                  SET Type=@Type, Name=@Name, DueDate=@DueDate, DateAdministered=@DateAdministered, 
                                      ValidityInterval=@ValidityInterval, ValidityUnit=@ValidityUnit, 
                                      MedicationIntervalHours=@MedicationIntervalHours, TimeStarted=@TimeStarted, 
                                      DosageTotal=@DosageTotal, DosageRemaining=@DosageRemaining,
                                      CheckupDate=@CheckupDate, DocumentPaths=@DocumentPaths,
                                      Completed=@Completed,
                                      CompletedAt=CASE
                                          WHEN @Completed = 1 AND CompletedAt IS NULL THEN NOW()
                                          WHEN @Completed = 0 THEN NULL
                                          ELSE CompletedAt
                                      END
                                  WHERE Id = @Id AND PetId = @PetId";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.Parameters.AddWithValue("@PetId", petId);
                        cmd.Parameters.AddWithValue("@Type", request.Type ?? "vaccine");
                        cmd.Parameters.AddWithValue("@Name", request.Name ?? "");
                        cmd.Parameters.AddWithValue("@DueDate", request.DueDate ?? "");
                        cmd.Parameters.AddWithValue("@DateAdministered", request.DateAdministered ?? "");
                        cmd.Parameters.AddWithValue("@ValidityInterval", request.ValidityInterval);
                        cmd.Parameters.AddWithValue("@ValidityUnit", request.ValidityUnit ?? "Months");
                        cmd.Parameters.AddWithValue("@MedicationIntervalHours", request.MedicationIntervalHours);
                        cmd.Parameters.AddWithValue("@TimeStarted", request.TimeStarted ?? "");
                        cmd.Parameters.AddWithValue("@DosageTotal", request.DosageTotal);
                        cmd.Parameters.AddWithValue("@DosageRemaining", request.DosageRemaining);
                        cmd.Parameters.AddWithValue("@CheckupDate", request.CheckupDate ?? "");
                        cmd.Parameters.AddWithValue("@DocumentPaths", request.DocumentPaths ?? "");

                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error updating health log: {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteHealthLog(int petId, int id)
        {
            try
            {
                string connString = _configuration.GetConnectionString("DefaultConnection")!;
                using (var connection = new MySqlConnection(connString))
                {
                    await connection.OpenAsync();
                    var query = "DELETE FROM healthlogs WHERE Id = @Id AND PetId = @PetId";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.Parameters.AddWithValue("@PetId", petId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error deleting health log: {ex.Message}");
            }
        }

        [HttpPut("{id}/complete")]
        public async Task<IActionResult> CompleteHealthLog(int petId, int id, [FromBody] CompleteHealthLogRequest req)
        {
            try
            {
                string connString = _configuration.GetConnectionString("DefaultConnection")!;
                using (var connection = new MySqlConnection(connString))
                {
                    await connection.OpenAsync();
                    var query = "UPDATE healthlogs SET Completed = 1, CompletedAt = NOW(), DosageRemaining = 0, DueDate = @NextDueDate WHERE Id = @Id AND PetId = @PetId";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.Parameters.AddWithValue("@PetId", petId);
                        cmd.Parameters.AddWithValue("@NextDueDate", req.NextDueDate ?? "");
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                return Ok(new { NextDueDate = req.NextDueDate });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error completing health log: {ex.Message}");
            }
        }
    }

    public class CompleteHealthLogRequest
    {
        public string NextDueDate { get; set; } = string.Empty;
    }

    public class HealthLogRequest
    {
        public string Type { get; set; } = "vaccine";
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
}

