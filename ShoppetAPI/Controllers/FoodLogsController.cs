using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Data;

namespace ShoppetAPI.Controllers
{
    [Route("api/pets/{petId}/[controller]")]
    [ApiController]
    public class FoodLogsController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public FoodLogsController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> GetFoodLogs(int petId)
        {
            try
            {
                string connString = _configuration.GetConnectionString("DefaultConnection")!;
                var logs = new List<object>();

                using (var connection = new MySqlConnection(connString))
                {
                    await connection.OpenAsync();
                    var query = "SELECT Id, PetId, FoodName, AmountGrams, IntervalHours, IntervalMinutes, StartTimestamp, LastFedTimestamp, FedDate, Notes, CreatedAt, IsCompleted, CompletedAt FROM foodlogs WHERE PetId = @PetId";

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
                                    FoodName = reader.GetString("FoodName"),
                                    AmountGrams = reader.GetDouble("AmountGrams"),
                                    IntervalHours = reader.GetInt32("IntervalHours"),
                                    IntervalMinutes = reader.GetInt32("IntervalMinutes"),
                                    StartTimestamp = reader.IsDBNull(reader.GetOrdinal("StartTimestamp")) ? "" : reader.GetString("StartTimestamp"),
                                    LastFedTimestamp = reader.IsDBNull(reader.GetOrdinal("LastFedTimestamp")) ? "" : reader.GetString("LastFedTimestamp"),
                                    FedDate = reader.IsDBNull(reader.GetOrdinal("FedDate")) ? "" : reader.GetString("FedDate"),
                                    Notes = reader.IsDBNull(reader.GetOrdinal("Notes")) ? "" : reader.GetString("Notes"),
                                    CreatedAt = reader.GetDateTime("CreatedAt"),
                                    IsCompleted = reader.IsDBNull(reader.GetOrdinal("IsCompleted")) ? false : reader.GetBoolean("IsCompleted"),
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
                return StatusCode(500, $"Error fetching food logs: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateFoodLog(int petId, [FromBody] FoodLogRequest request)
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
                    var query = @"INSERT INTO foodlogs
                                  (PetId, FoodName, AmountGrams, IntervalHours, IntervalMinutes,
                                   StartTimestamp, LastFedTimestamp, FedDate, Notes, CreatedAt, IsCompleted)
                                  VALUES
                                  (@PetId, @FoodName, @AmountGrams, @IntervalHours, @IntervalMinutes,
                                   @StartTimestamp, @LastFedTimestamp, @FedDate, @Notes, NOW(), 0)";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@PetId", petId);
                        cmd.Parameters.AddWithValue("@FoodName", request.FoodName ?? "");
                        cmd.Parameters.AddWithValue("@AmountGrams", request.AmountGrams);
                        cmd.Parameters.AddWithValue("@IntervalHours", request.IntervalHours);
                        cmd.Parameters.AddWithValue("@IntervalMinutes", request.IntervalMinutes);
                        cmd.Parameters.AddWithValue("@StartTimestamp", request.StartTimestamp ?? "");
                        cmd.Parameters.AddWithValue("@LastFedTimestamp", request.LastFedTimestamp ?? "");
                        cmd.Parameters.AddWithValue("@FedDate", request.FedDate ?? "");
                        cmd.Parameters.AddWithValue("@Notes", request.Notes ?? "");

                        await cmd.ExecuteNonQueryAsync();
                        newId = cmd.LastInsertedId;
                    }
                }

                return Ok(new { Id = (int)newId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error saving food log: {ex.Message}");
            }
        }

                [HttpPut("{id}")]
        public async Task<IActionResult> UpdateFoodLog(int petId, int id, [FromBody] FoodLogRequest request)
        {
            try
            {
                string connString = _configuration.GetConnectionString("DefaultConnection")!;
                using (var connection = new MySqlConnection(connString))
                {
                    await connection.OpenAsync();
                    var query = @"UPDATE foodlogs 
                                  SET FoodName=@FoodName, AmountGrams=@AmountGrams, 
                                      IntervalHours=@IntervalHours, IntervalMinutes=@IntervalMinutes,
                                      StartTimestamp=@StartTimestamp,
                                      LastFedTimestamp=@LastFedTimestamp,
                                      FedDate=@FedDate,
                                      Notes=@Notes
                                  WHERE Id = @Id AND PetId = @PetId";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.Parameters.AddWithValue("@PetId", petId);
                        cmd.Parameters.AddWithValue("@FoodName", request.FoodName ?? "");
                        cmd.Parameters.AddWithValue("@AmountGrams", request.AmountGrams);
                        cmd.Parameters.AddWithValue("@IntervalHours", request.IntervalHours);
                        cmd.Parameters.AddWithValue("@IntervalMinutes", request.IntervalMinutes);
                        cmd.Parameters.AddWithValue("@StartTimestamp", request.StartTimestamp ?? "");
                        cmd.Parameters.AddWithValue("@LastFedTimestamp", request.LastFedTimestamp ?? "");
                        cmd.Parameters.AddWithValue("@FedDate", request.FedDate ?? "");
                        cmd.Parameters.AddWithValue("@Notes", request.Notes ?? "");

                        int rows = await cmd.ExecuteNonQueryAsync();
                        if (rows == 0)
                            return NotFound("Food log not found.");
                    }
                }
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error updating food log: {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFoodLog(int petId, int id)
        {
            try
            {
                string connString = _configuration.GetConnectionString("DefaultConnection")!;
                using (var connection = new MySqlConnection(connString))
                {
                    await connection.OpenAsync();
                    var query = "DELETE FROM foodlogs WHERE Id = @Id AND PetId = @PetId";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.Parameters.AddWithValue("@PetId", petId);
                        int rows = await cmd.ExecuteNonQueryAsync();
                        if (rows == 0)
                            return NotFound("Food log not found.");
                    }
                }

                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error deleting food log: {ex.Message}");
            }
        }

        [HttpPut("{id}/complete")]
        public async Task<IActionResult> CompleteFoodLog(int petId, int id)
        {
            try
            {
                string connString = _configuration.GetConnectionString("DefaultConnection")!;
                string lastFed = DateTime.Now.ToString("o"); // ISO 8601
                using (var connection = new MySqlConnection(connString))
                {
                    await connection.OpenAsync();
                    var query = "UPDATE foodlogs SET IsCompleted = 1, CompletedAt = NOW(), LastFedTimestamp = @LastFed WHERE Id = @Id AND PetId = @PetId";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.Parameters.AddWithValue("@PetId", petId);
                        cmd.Parameters.AddWithValue("@LastFed", lastFed);
                        int rows = await cmd.ExecuteNonQueryAsync();
                        if (rows == 0)
                            return NotFound("Food log not found.");
                    }
                }

                return Ok(new { LastFedTimestamp = lastFed });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error completing food log: {ex.Message}");
            }
        }
    }

    public class FoodLogRequest
    {
        public string FoodName { get; set; } = string.Empty;
        public double AmountGrams { get; set; }
        public int IntervalHours { get; set; }
        public int IntervalMinutes { get; set; }
        public string StartTimestamp { get; set; } = string.Empty;
        public string LastFedTimestamp { get; set; } = string.Empty;
        public string FedDate { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }
}

