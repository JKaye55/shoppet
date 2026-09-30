using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ShoppetAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProfileController : ControllerBase
    {
        private readonly IConfiguration _config;

        public ProfileController(IConfiguration config)
        {
            _config = config;
        }

        private string SharedConnectionString =>
            _config.GetConnectionString("SharedSqlServer")
            ?? throw new InvalidOperationException(
                "Connection string 'SharedSqlServer' was not found.");

        public sealed class UpdateProfileRequest
        {
            public int UserId { get; set; }
            public string FullName { get; set; } = string.Empty;

            // Kept in the request contract so the current MAUI UI does not
            // break while ProfilePicture is added to the shared SQL schema
            // in the next profile/social integration phase.
            public string ProfilePictureBase64 { get; set; } = string.Empty;
        }

        [HttpGet("{userId:int}")]
        public async Task<IActionResult> GetProfile(int userId)
        {
            if (userId <= 0)
                return BadRequest("A valid user ID is required.");

            try
            {
                await using var conn = new SqlConnection(SharedConnectionString);
                await conn.OpenAsync();

                await using var cmd = new SqlCommand(@"
                    SELECT
                        FullName,
                        Email,
                        MobileNumber
                    FROM UserAccounts
                    WHERE Id = @UserId;", conn);

                cmd.Parameters.AddWithValue("@UserId", userId);

                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return NotFound();

                return Ok(new
                {
                    FullName = reader.IsDBNull(reader.GetOrdinal("FullName"))
                        ? string.Empty
                        : reader.GetString(reader.GetOrdinal("FullName")),
                    Email = reader.IsDBNull(reader.GetOrdinal("Email"))
                        ? string.Empty
                        : reader.GetString(reader.GetOrdinal("Email")),
                    MobileNumber = reader.IsDBNull(reader.GetOrdinal("MobileNumber"))
                        ? string.Empty
                        : reader.GetString(reader.GetOrdinal("MobileNumber")),
                    ProfilePicture = string.Empty
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Profile read error: {ex.Message}");
            }
        }

        [HttpPut("update")]
        public async Task<IActionResult> UpdateProfile(
            [FromBody] UpdateProfileRequest request)
        {
            if (request.UserId <= 0)
                return BadRequest("A valid user ID is required.");

            if (string.IsNullOrWhiteSpace(request.FullName))
                return BadRequest("Full name is required.");

            try
            {
                await using var conn = new SqlConnection(SharedConnectionString);
                await conn.OpenAsync();

                await using var cmd = new SqlCommand(@"
                    UPDATE UserAccounts
                    SET FullName = @FullName
                    WHERE Id = @UserId;", conn);

                cmd.Parameters.AddWithValue(
                    "@FullName",
                    request.FullName.Trim());
                cmd.Parameters.AddWithValue(
                    "@UserId",
                    request.UserId);

                int rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0)
                    return NotFound("User not found.");

                return Ok(new
                {
                    success = true,
                    FullName = request.FullName.Trim()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Profile update error: {ex.Message}");
            }
        }
    }
}
