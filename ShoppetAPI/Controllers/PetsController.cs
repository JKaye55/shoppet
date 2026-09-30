using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Globalization;

namespace ShoppetAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PetsController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public PetsController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private string ConnectionString =>
        _configuration.GetConnectionString("SharedSqlServer")
        ?? throw new InvalidOperationException("Connection string 'SharedSqlServer' was not found.");

    [HttpGet]
    public async Task<IActionResult> GetPets([FromQuery] int? userId = null)
    {
        try
        {
            var pets = new List<object>();

            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            var query = """
                SELECT
                    Id,
                    UserId,
                    PetName,
                    Species,
                    Breed,
                    Age,
                    WeightKg,
                    ISNULL(Diet,'') AS Diet,
                    CreatedAt,
                    ISNULL(CardId,'') AS CardId,
                    CardIssuedAt,
                    ISNULL(CardTheme,'') AS CardTheme,
                    CASE
                        WHEN COL_LENGTH('dbo.PetProfiles', 'PhotoUrl') IS NULL THEN ''
                        ELSE ISNULL(PhotoUrl, '')
                    END AS PhotoUrl
                FROM PetProfiles
                """;

            if (userId.HasValue)
                query += " WHERE UserId=@UserId";

            query += " ORDER BY PetName;";

            await using var cmd = new SqlCommand(query, connection);
            if (userId.HasValue)
                cmd.Parameters.AddWithValue("@UserId", userId.Value);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var ageText = reader.IsDBNull(5) ? string.Empty : reader.GetString(5);
                var ageYears = ParseAgeYears(ageText);
                var weight = reader.IsDBNull(6)
                    ? string.Empty
                    : $"{reader.GetDecimal(6):0.##} kg";

                pets.Add(new
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    Name = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Species = reader.IsDBNull(3) ? "Dog" : reader.GetString(3),
                    Breed = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    AgeYears = ageYears,
                    Weight = weight,
                    Diet = reader.GetString(7),
                    CreatedAt = reader.GetDateTime(8),
                    CardId = reader.GetString(9),
                    CardIssuedAt = reader.IsDBNull(10) ? (DateTime?)null : reader.GetDateTime(10),
                    CardTheme = reader.GetString(11),
                    PhotoUrl = reader.IsDBNull(12) ? string.Empty : reader.GetString(12)
                });
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
        if (request.UserId <= 0 || string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("A valid owner and pet name are required.");

        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            var cardId = "PET-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            var weightKg = ParseWeight(request.Weight);

            const string sql = """
                INSERT INTO PetProfiles
                    (UserId, PetName, Species, Breed, Age, WeightKg, Diet, CardId, CardIssuedAt, CardTheme, PhotoUrl)
                OUTPUT INSERTED.Id
                VALUES
                    (@UserId, @PetName, @Species, @Breed, @Age, @WeightKg, @Diet, @CardId, SYSDATETIME(), @CardTheme, @PhotoUrl);
                """;

            await using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@UserId", request.UserId);
            cmd.Parameters.AddWithValue("@PetName", request.Name.Trim());
            cmd.Parameters.AddWithValue("@Species", request.Species?.Trim() ?? "Dog");
            cmd.Parameters.AddWithValue("@Breed", request.Breed?.Trim() ?? string.Empty);
            cmd.Parameters.AddWithValue("@Age", request.AgeYears > 0 ? request.AgeYears.ToString(CultureInfo.InvariantCulture) : DBNull.Value);
            cmd.Parameters.AddWithValue("@WeightKg", weightKg.HasValue ? weightKg.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@Diet", request.Diet?.Trim() ?? string.Empty);
            cmd.Parameters.AddWithValue("@CardId", cardId);
            cmd.Parameters.AddWithValue("@CardTheme", request.CardTheme?.Trim() ?? string.Empty);
            cmd.Parameters.AddWithValue("@PhotoUrl", request.PhotoUrl?.Trim() ?? string.Empty);

            var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

            return Ok(new
            {
                Id = newId,
                request.UserId,
                Name = request.Name.Trim(),
                Species = request.Species?.Trim() ?? "Dog",
                Breed = request.Breed?.Trim() ?? string.Empty,
                request.AgeYears,
                Weight = request.Weight ?? string.Empty,
                Diet = request.Diet ?? string.Empty,
                CardId = cardId,
                CardIssuedAt = DateTime.Now,
                CardTheme = request.CardTheme ?? string.Empty,
                PhotoUrl = request.PhotoUrl ?? string.Empty
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error saving pet: {ex.Message}");
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdatePet(int id, [FromBody] PetCreateRequest request)
    {
        if (request.UserId <= 0)
            return BadRequest("A valid owner is required.");

        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            const string sql = """
                UPDATE PetProfiles
                SET PetName=@PetName,
                    Species=@Species,
                    Breed=@Breed,
                    Age=@Age,
                    WeightKg=@WeightKg,
                    Diet=@Diet,
                    CardTheme=@CardTheme,
                    PhotoUrl=@PhotoUrl
                WHERE Id=@Id AND UserId=@UserId;
                """;

            await using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@UserId", request.UserId);
            cmd.Parameters.AddWithValue("@PetName", request.Name?.Trim() ?? string.Empty);
            cmd.Parameters.AddWithValue("@Species", request.Species?.Trim() ?? "Dog");
            cmd.Parameters.AddWithValue("@Breed", request.Breed?.Trim() ?? string.Empty);
            cmd.Parameters.AddWithValue("@Age", request.AgeYears > 0 ? request.AgeYears.ToString(CultureInfo.InvariantCulture) : DBNull.Value);
            cmd.Parameters.AddWithValue("@WeightKg", ParseWeight(request.Weight) is decimal w ? w : DBNull.Value);
            cmd.Parameters.AddWithValue("@Diet", request.Diet?.Trim() ?? string.Empty);
            cmd.Parameters.AddWithValue("@CardTheme", request.CardTheme?.Trim() ?? string.Empty);
            cmd.Parameters.AddWithValue("@PhotoUrl", request.PhotoUrl?.Trim() ?? string.Empty);

            var rows = await cmd.ExecuteNonQueryAsync();
            return rows == 0 ? NotFound() : Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error updating pet: {ex.Message}");
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeletePet(int id, [FromQuery] int userId = 0)
    {
        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

            if (userId > 0)
            {
                await using var ownerCheck = new SqlCommand(
                    "SELECT COUNT(1) FROM PetProfiles WHERE Id=@Id AND UserId=@UserId",
                    connection,
                    transaction);
                ownerCheck.Parameters.AddWithValue("@Id", id);
                ownerCheck.Parameters.AddWithValue("@UserId", userId);

                if (Convert.ToInt32(await ownerCheck.ExecuteScalarAsync()) == 0)
                    return NotFound("Pet not found.");
            }

            var cleanupSql = new[]
            {
                "DELETE FROM PetHealthRecords WHERE PetId=@Id",
                "DELETE FROM FoodLogs WHERE PetId=@Id",
                "UPDATE CommunityPosts SET PetId=NULL WHERE PetId=@Id"
            };

            foreach (var sql in cleanupSql)
            {
                await using var cleanup = new SqlCommand(sql, connection, transaction);
                cleanup.Parameters.AddWithValue("@Id", id);
                await cleanup.ExecuteNonQueryAsync();
            }

            var deleteSql = userId > 0
                ? "DELETE FROM PetProfiles WHERE Id=@Id AND UserId=@UserId"
                : "DELETE FROM PetProfiles WHERE Id=@Id";

            await using var cmd = new SqlCommand(deleteSql, connection, transaction);
            cmd.Parameters.AddWithValue("@Id", id);
            if (userId > 0)
                cmd.Parameters.AddWithValue("@UserId", userId);

            var rows = await cmd.ExecuteNonQueryAsync();
            if (rows == 0)
            {
                await transaction.RollbackAsync();
                return NotFound("Pet not found.");
            }

            await transaction.CommitAsync();
            return Ok(new { message = "Pet deleted successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error deleting pet: {ex.Message}");
        }
    }

    private static int ParseAgeYears(string? age)
    {
        if (string.IsNullOrWhiteSpace(age)) return 0;
        var first = age.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return int.TryParse(first, out var value) ? value : 0;
    }

    private static decimal? ParseWeight(string? weight)
    {
        if (string.IsNullOrWhiteSpace(weight)) return null;

        var cleaned = new string(weight
            .Where(c => char.IsDigit(c) || c == '.' || c == ',')
            .ToArray())
            .Replace(',', '.');

        return decimal.TryParse(
            cleaned,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : null;
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
    public string Diet { get; set; } = string.Empty;
    public string CardTheme { get; set; } = string.Empty;
}
