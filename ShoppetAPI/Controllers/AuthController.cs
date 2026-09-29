using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Data;

namespace ShoppetAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public AuthController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.FullName) ||
                    string.IsNullOrWhiteSpace(request.Email) ||
                    string.IsNullOrWhiteSpace(request.Password))
                {
                    return BadRequest("Full name, email, and password are required.");
                }

                string normalizedEmail = request.Email.Trim();
                string connString = _configuration.GetConnectionString("DefaultConnection")!;

                using (var connection = new MySqlConnection(connString))
                {
                    await connection.OpenAsync();

                    var checkCmd = new MySqlCommand("SELECT COUNT(*) FROM Users WHERE Email = @Email", connection);
                    checkCmd.Parameters.AddWithValue("@Email", normalizedEmail);
                    long count = (long)await checkCmd.ExecuteScalarAsync();

                    if (count > 0)
                    {
                        return Conflict("Email is already registered.");
                    }

                    string hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

                    var insertCmd = new MySqlCommand(@"
                        INSERT INTO Users (FullName, Email, PasswordHash, CreatedAt, Role)
                        VALUES (@FullName, @Email, @PasswordHash, NOW(), @Role)", connection);

                    insertCmd.Parameters.AddWithValue("@FullName", request.FullName.Trim());
                    insertCmd.Parameters.AddWithValue("@Email", normalizedEmail);
                    insertCmd.Parameters.AddWithValue("@PasswordHash", hashedPassword);
                    insertCmd.Parameters.AddWithValue("@Role", "PetOwner");

                    await insertCmd.ExecuteNonQueryAsync();
                    int newUserId = Convert.ToInt32(insertCmd.LastInsertedId);

                    return Ok(new AuthResponse
                    {
                        UserId = newUserId,
                        FullName = request.FullName.Trim(),
                        Email = normalizedEmail,
                        Role = "PetOwner",
                        ProfilePicture = string.Empty,
                        Token = "sample-token"
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Registration error: {ex.Message}");
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Email) ||
                    string.IsNullOrWhiteSpace(request.Password))
                {
                    return BadRequest("Email and password are required.");
                }

                string normalizedEmail = request.Email.Trim();
                string connString = _configuration.GetConnectionString("DefaultConnection")!;

                using (var connection = new MySqlConnection(connString))
                {
                    await connection.OpenAsync();

                    var cmd = new MySqlCommand("SELECT Id, FullName, Email, PasswordHash, Role, ProfilePicture FROM Users WHERE Email = @Email", connection);
                    cmd.Parameters.AddWithValue("@Email", normalizedEmail);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            int userId = reader.GetInt32("Id");
                            string fullName = reader.GetString("FullName");
                            string email = reader.GetString("Email");
                            string passwordHash = reader.GetString("PasswordHash");
                            string role = reader.IsDBNull(reader.GetOrdinal("Role"))
                                ? "PetOwner"
                                : reader.GetString("Role");
                            string profilePicture = reader.IsDBNull(reader.GetOrdinal("ProfilePicture"))
                                ? string.Empty
                                : reader.GetString("ProfilePicture");

                            bool isBcryptHash =
                                passwordHash.StartsWith("$2a$", StringComparison.Ordinal) ||
                                passwordHash.StartsWith("$2b$", StringComparison.Ordinal) ||
                                passwordHash.StartsWith("$2y$", StringComparison.Ordinal);

                            bool isValidPassword = isBcryptHash
                                ? BCrypt.Net.BCrypt.Verify(request.Password, passwordHash)
                                : string.Equals(
                                    request.Password,
                                    passwordHash,
                                    StringComparison.Ordinal);

                            if (isValidPassword)
                            {
                                // One-time migration for legacy imported accounts that still
                                // contain a plain-text password in the PasswordHash column.
                                if (!isBcryptHash)
                                {
                                    await reader.DisposeAsync();

                                    string upgradedHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
                                    using var migrateCmd = new MySqlCommand(
                                        "UPDATE Users SET PasswordHash = @PasswordHash WHERE Id = @Id",
                                        connection);
                                    migrateCmd.Parameters.AddWithValue("@PasswordHash", upgradedHash);
                                    migrateCmd.Parameters.AddWithValue("@Id", userId);
                                    await migrateCmd.ExecuteNonQueryAsync();
                                }

                                return Ok(new AuthResponse
                                {
                                    UserId = userId,
                                    FullName = fullName,
                                    Email = email,
                                    Role = role,
                                    ProfilePicture = profilePicture,
                                    Token = "sample-token"
                                });
                            }
                        }
                    }

                    return Unauthorized("Invalid email or password.");
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Login error: {ex.Message}");
            }
        }
    }

    public class RegisterRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class AuthResponse
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = "PetOwner";
        public string ProfilePicture { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
    }
}