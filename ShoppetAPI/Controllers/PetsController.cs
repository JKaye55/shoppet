using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Data;

namespace ShoppetAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PetsController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public PetsController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> GetPets([FromQuery] int? userId = null)
        {
            try
            {
                string connString = _configuration.GetConnectionString("DefaultConnection")!;
                var pets = new List<object>();

                using (var connection = new MySqlConnection(connString))
                {
                    await connection.OpenAsync();
                    var query = "SELECT Id, UserId, Name, Species, Breed, AgeYears, Weight, PhotoUrl, CreatedAt FROM pets";
                    
                    if (userId.HasValue)
                    {
                        query += " WHERE UserId = @userId";
                    }

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        if (userId.HasValue)
                        {
                            cmd.Parameters.AddWithValue("@userId", userId.Value);
                        }

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                        {
                            pets.Add(new
                            {
                                Id = reader.GetInt32("Id"),
                                UserId = reader.GetInt32("UserId"),
                                Name = reader.GetString("Name"),
                                Species = reader.GetString("Species"),
                                Breed = reader.GetString("Breed"),
                                AgeYears = reader.GetInt32("AgeYears"),
                                Weight = reader.IsDBNull(reader.GetOrdinal("Weight")) ? "" : reader.GetString("Weight"),
                                PhotoUrl = reader.IsDBNull(reader.GetOrdinal("PhotoUrl")) ? "" : reader.GetString("PhotoUrl"),
                                CreatedAt = reader.GetDateTime("CreatedAt")
                            });
                        }
                    }
                }
            }

                return Ok(pets);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error fetching pets: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreatePet([FromBody] PetCreateRequest request)
        {
            try
            {
                if (request.UserId <= 0)
                    return BadRequest("A valid user ID is required.");

                string connString = _configuration.GetConnectionString("DefaultConnection")!;

                using (var connection = new MySqlConnection(connString))
                {
                    await connection.OpenAsync();

                    var query = @"INSERT INTO pets (UserId, Name, Species, Breed, AgeYears, Weight, PhotoUrl, CreatedAt) 
                                  VALUES (@UserId, @Name, @Species, @Breed, @AgeYears, @Weight, @PhotoUrl, @CreatedAt)";

                    long newId = 0;
                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@UserId", request.UserId);
                        cmd.Parameters.AddWithValue("@Name", request.Name ?? string.Empty);
                        cmd.Parameters.AddWithValue("@Species", request.Species ?? "Dog");
                        cmd.Parameters.AddWithValue("@Breed", request.Breed ?? string.Empty);
                        cmd.Parameters.AddWithValue("@AgeYears", request.AgeYears);
                        cmd.Parameters.AddWithValue("@Weight", request.Weight ?? string.Empty);
                        cmd.Parameters.AddWithValue("@PhotoUrl", request.PhotoUrl ?? string.Empty);
                        cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);

                        await cmd.ExecuteNonQueryAsync();
                        newId = cmd.LastInsertedId;
                    }

                    // Return the newly created pet object so the mobile app can update its local ID
                    var createdPet = new
                    {
                        Id = (int)newId,
                        UserId = request.UserId,
                        Name = request.Name,
                        Species = request.Species,
                        Breed = request.Breed,
                        AgeYears = request.AgeYears,
                        Weight = request.Weight,
                        PhotoUrl = request.PhotoUrl
                    };

                    return Ok(createdPet);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error saving pet: {ex.Message}");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePet(int id, [FromBody] PetCreateRequest request)
        {
            try
            {
                string connString = _configuration.GetConnectionString("DefaultConnection")!;

                using (var connection = new MySqlConnection(connString))
                {
                    await connection.OpenAsync();

                    var query = @"UPDATE pets
                                  SET Name = @Name,
                                      Species = @Species,
                                      Breed = @Breed,
                                      AgeYears = @AgeYears,
                                      Weight = @Weight,
                                      PhotoUrl = @PhotoUrl
                                  WHERE Id = @Id AND UserId = @UserId";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.Parameters.AddWithValue("@UserId", request.UserId);
                        cmd.Parameters.AddWithValue("@Name", request.Name ?? string.Empty);
                        cmd.Parameters.AddWithValue("@Species", request.Species ?? "Dog");
                        cmd.Parameters.AddWithValue("@Breed", request.Breed ?? string.Empty);
                        cmd.Parameters.AddWithValue("@AgeYears", request.AgeYears);
                        cmd.Parameters.AddWithValue("@Weight", request.Weight ?? string.Empty);
                        cmd.Parameters.AddWithValue("@PhotoUrl", request.PhotoUrl ?? string.Empty);

                        int rowsAffected = await cmd.ExecuteNonQueryAsync();

                        if (rowsAffected == 0)
                            return NotFound("Pet not found.");

                        return Ok(new
                        {
                            Id = id,
                            UserId = request.UserId,
                            request.Name,
                            request.Species,
                            request.Breed,
                            request.AgeYears,
                            request.Weight,
                            request.PhotoUrl
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error updating pet: {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePet(int id)
        {
            try
            {
                string connString = _configuration.GetConnectionString("DefaultConnection")!;
                using (var connection = new MySqlConnection(connString))
                {
                    await connection.OpenAsync();
                    var cmd = new MySqlCommand("DELETE FROM pets WHERE Id = @Id", connection);
                    cmd.Parameters.AddWithValue("@Id", id);
                    int rowsAffected = await cmd.ExecuteNonQueryAsync();

                    if (rowsAffected > 0)
                        return Ok(new { message = "Pet deleted successfully" });

                    return NotFound("Pet not found.");
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error deleting pet: {ex.Message}");
            }
        }
    }

    public class PetCreateRequest
    {
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Species { get; set; } = string.Empty;
        public string Breed { get; set; } = string.Empty;
        public int AgeYears { get; set; }
        public string Weight { get; set; } = string.Empty;
        public string PhotoUrl { get; set; } = string.Empty;
    }
}