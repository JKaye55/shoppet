using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Globalization;

namespace ShoppetAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PetsController : ControllerBase
{
    private readonly IConfiguration _configuration;
    public PetsController(IConfiguration configuration) => _configuration = configuration;

    private string ConnectionString =>
        _configuration.GetConnectionString("SharedSqlServer")
        ?? throw new InvalidOperationException("SharedSqlServer connection string not found.");

    [HttpGet]
    public async Task<IActionResult> GetPets([FromQuery] int? userId = null)
    {
        try
        {
            var pets = new List<object>();
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            string sql = @"SELECT Id, UserId, PetName, Species, Breed, Age, WeightKg, PhotoUrl, CreatedAt
                           FROM PetProfiles";
            if (userId.HasValue) sql += " WHERE UserId=@UserId";
            sql += " ORDER BY PetName";

            await using var cmd = new SqlCommand(sql, connection);
            if (userId.HasValue) cmd.Parameters.AddWithValue("@UserId", userId.Value);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                int age = 0;
                if (!reader.IsDBNull(reader.GetOrdinal("Age")))
                    int.TryParse(reader.GetString(reader.GetOrdinal("Age")), out age);

                string weight = "";
                if (!reader.IsDBNull(reader.GetOrdinal("WeightKg")))
                    weight = reader.GetDecimal(reader.GetOrdinal("WeightKg")).ToString(CultureInfo.InvariantCulture);

                pets.Add(new
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                    Name = reader.GetString(reader.GetOrdinal("PetName")),
                    Species = reader.GetString(reader.GetOrdinal("Species")),
                    Breed = reader.GetString(reader.GetOrdinal("Breed")),
                    AgeYears = age,
                    Weight = weight,
                    PhotoUrl = reader.IsDBNull(reader.GetOrdinal("PhotoUrl")) ? "" : reader.GetString(reader.GetOrdinal("PhotoUrl")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
                });
            }

            return Ok(pets);
        }
        catch (Exception ex) { return StatusCode(500, $"Error fetching pets: {ex.Message}"); }
    }

    [HttpPost]
    public async Task<IActionResult> CreatePet([FromBody] PetCreateRequest request)
    {
        if (request.UserId <= 0) return BadRequest("A valid user ID is required.");

        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            decimal? weight = decimal.TryParse(request.Weight, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedWeight)
                ? parsedWeight : null;

            await using var cmd = new SqlCommand(@"
                INSERT INTO PetProfiles
                    (UserId, PetName, Breed, Species, Age, WeightKg, Diet, CreatedAt, PhotoUrl)
                OUTPUT INSERTED.Id
                VALUES
                    (@UserId, @Name, @Breed, @Species, @Age, @WeightKg, '', SYSDATETIME(), @PhotoUrl);", connection);

            cmd.Parameters.AddWithValue("@UserId", request.UserId);
            cmd.Parameters.AddWithValue("@Name", request.Name ?? "");
            cmd.Parameters.AddWithValue("@Breed", request.Breed ?? "");
            cmd.Parameters.AddWithValue("@Species", request.Species ?? "Dog");
            cmd.Parameters.AddWithValue("@Age", request.AgeYears.ToString());
            cmd.Parameters.AddWithValue("@WeightKg", (object?)weight ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PhotoUrl", string.IsNullOrWhiteSpace(request.PhotoUrl) ? DBNull.Value : request.PhotoUrl);

            int id = Convert.ToInt32(await cmd.ExecuteScalarAsync());

            return Ok(new
            {
                Id = id,
                request.UserId,
                request.Name,
                request.Species,
                request.Breed,
                request.AgeYears,
                request.Weight,
                request.PhotoUrl
            });
        }
        catch (Exception ex) { return StatusCode(500, $"Error saving pet: {ex.Message}"); }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdatePet(int id, [FromBody] PetCreateRequest request)
    {
        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            decimal? weight = decimal.TryParse(request.Weight, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedWeight)
                ? parsedWeight : null;

            await using var cmd = new SqlCommand(@"
                UPDATE PetProfiles
                SET PetName=@Name,
                    Species=@Species,
                    Breed=@Breed,
                    Age=@Age,
                    WeightKg=@WeightKg,
                    PhotoUrl=@PhotoUrl
                WHERE Id=@Id AND UserId=@UserId;", connection);

            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@UserId", request.UserId);
            cmd.Parameters.AddWithValue("@Name", request.Name ?? "");
            cmd.Parameters.AddWithValue("@Species", request.Species ?? "Dog");
            cmd.Parameters.AddWithValue("@Breed", request.Breed ?? "");
            cmd.Parameters.AddWithValue("@Age", request.AgeYears.ToString());
            cmd.Parameters.AddWithValue("@WeightKg", (object?)weight ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PhotoUrl", string.IsNullOrWhiteSpace(request.PhotoUrl) ? DBNull.Value : request.PhotoUrl);

            if (await cmd.ExecuteNonQueryAsync() == 0) return NotFound("Pet not found.");
            return Ok(new { Id = id, request.UserId, request.Name, request.Species, request.Breed, request.AgeYears, request.Weight, request.PhotoUrl });
        }
        catch (Exception ex) { return StatusCode(500, $"Error updating pet: {ex.Message}"); }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeletePet(int id)
    {
        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var cmd = new SqlCommand("DELETE FROM PetProfiles WHERE Id=@Id", connection);
            cmd.Parameters.AddWithValue("@Id", id);
            return await cmd.ExecuteNonQueryAsync() > 0
                ? Ok(new { message = "Pet deleted successfully" })
                : NotFound("Pet not found.");
        }
        catch (Exception ex) { return StatusCode(500, $"Error deleting pet: {ex.Message}"); }
    }
}

public class PetCreateRequest
{
    public int UserId { get; set; }
    public string Name { get; set; } = "";
    public string Species { get; set; } = "";
    public string Breed { get; set; } = "";
    public int AgeYears { get; set; }
    public string Weight { get; set; } = "";
    public string PhotoUrl { get; set; } = "";
}
