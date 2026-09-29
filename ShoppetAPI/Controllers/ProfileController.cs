using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace ShoppetAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProfileController : ControllerBase
    {
        private readonly IConfiguration _config;
        public ProfileController(IConfiguration config) => _config = config;

        public class UpdateProfileRequest
        {
            public int UserId { get; set; }
            public string FullName { get; set; } = string.Empty;
            public string ProfilePictureBase64 { get; set; } = string.Empty;
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetProfile(int userId)
        {
            if (userId <= 0)
                return BadRequest("A valid user ID is required.");

            try
            {
                using var conn = new MySqlConnection(_config.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();
                                var query = @"
                    SELECT FullName, ProfilePicture
                    FROM users 
                    WHERE Id = @UserId";
                using var cmd = new MySqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@UserId", userId);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return Ok(new {
                        FullName = reader["FullName"].ToString(),
                        ProfilePicture = reader["ProfilePicture"]?.ToString() ?? string.Empty
                    });
                }
                return NotFound();
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        [HttpPut("update")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req)
        {
            if (req.UserId <= 0)
                return BadRequest("A valid user ID is required.");

            if (string.IsNullOrWhiteSpace(req.FullName))
                return BadRequest("Full name is required.");

            try
            {
                using var conn = new MySqlConnection(_config.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();
                
                try
                {
                    // Update users table directly
                    var q1 = "UPDATE users SET FullName = @FullName, ProfilePicture = @Pic WHERE Id = @UserId";
                    using var c1 = new MySqlCommand(q1, conn);
                    c1.Parameters.AddWithValue("@FullName", req.FullName);
                    c1.Parameters.AddWithValue("@Pic", req.ProfilePictureBase64);
                    c1.Parameters.AddWithValue("@UserId", req.UserId);
                    int rows = await c1.ExecuteNonQueryAsync();

                    if (rows == 0)
                        return NotFound("User not found.");

                    return Ok(new { success = true });
                }
                catch
                {
                    throw;
                }
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }
    }
}





