using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ShoppetAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly PasswordHasher<SharedAuthUser> _passwordHasher = new();

        public AuthController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private string SharedConnectionString =>
            _configuration.GetConnectionString("SharedSqlServer")
            ?? throw new InvalidOperationException(
                "Connection string 'SharedSqlServer' was not found.");

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FullName) ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest("Full name, email, and password are required.");
            }

            string fullName = request.FullName.Trim();
            string email = request.Email.Trim().ToLowerInvariant();

            if(fullName.Length<2||fullName.Length>80)
                return BadRequest("Full name must be between 2 and 80 characters.");

            if(email.Length>254||!System.Net.Mail.MailAddress.TryCreate(email,out _))
                return BadRequest("Enter a valid email address.");

            if(request.Password.Length<8||request.Password.Length>128||
               !request.Password.Any(char.IsUpper)||
               !request.Password.Any(char.IsLower)||
               !request.Password.Any(char.IsDigit))
                return BadRequest("Password must be 8–128 characters and include uppercase, lowercase, and a number.");

            try
            {
                await using var connection = new SqlConnection(SharedConnectionString);
                await connection.OpenAsync();

                await using (var checkCmd = new SqlCommand(
                    @"SELECT COUNT(1)
                      FROM UserAccounts
                      WHERE LOWER(Email) = @Email;", connection))
                {
                    checkCmd.Parameters.AddWithValue("@Email", email);
                    int count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

                    if (count > 0)
                        return Conflict("Email is already registered.");
                }

                var user = new SharedAuthUser
                {
                    FullName = fullName,
                    Email = email,
                    Role = "Pet Owner"
                };

                string passwordHash =
                    _passwordHasher.HashPassword(user, request.Password);

                await using var insertCmd = new SqlCommand(@"
                    INSERT INTO UserAccounts
                        (FullName, Email, PasswordHash, Role, MobileNumber)
                    OUTPUT
                        INSERTED.Id,
                        INSERTED.CreatedAt
                    VALUES
                        (@FullName, @Email, @PasswordHash, @Role, NULL);",
                    connection);

                insertCmd.Parameters.AddWithValue("@FullName", fullName);
                insertCmd.Parameters.AddWithValue("@Email", email);
                insertCmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
                insertCmd.Parameters.AddWithValue("@Role", "Pet Owner");

                await using var reader = await insertCmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return StatusCode(500, "Registration did not return a new user.");

                int newUserId = reader.GetInt32(0);

                return Ok(new AuthResponse
                {
                    UserId = newUserId,
                    FullName = fullName,
                    Email = email,
                    Role = "Pet Owner",
                    ProfilePicture = string.Empty,
                    Token = Guid.NewGuid().ToString("N")
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Registration error: {ex.Message}");
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest("Email and password are required.");
            }

            string email = request.Email.Trim().ToLowerInvariant();

            if(email.Length>254||!System.Net.Mail.MailAddress.TryCreate(email,out _))
                return BadRequest("Enter a valid email address.");
            if(request.Password.Length>128)
                return BadRequest("Password is too long.");

            try
            {
                await using var connection = new SqlConnection(SharedConnectionString);
                await connection.OpenAsync();

                await using var cmd = new SqlCommand(@"
                    SELECT
                        Id,
                        FullName,
                        Email,
                        PasswordHash,
                        Role,
                        MobileNumber
                    FROM UserAccounts
                    WHERE LOWER(Email) = @Email;", connection);

                cmd.Parameters.AddWithValue("@Email", email);

                int userId;
                string fullName;
                string storedCredential;
                string role;
                string mobileNumber;

                await using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                        return Unauthorized("Invalid email or password.");

                    userId = reader.GetInt32(reader.GetOrdinal("Id"));
                    fullName = reader.IsDBNull(reader.GetOrdinal("FullName"))
                        ? string.Empty
                        : reader.GetString(reader.GetOrdinal("FullName"));
                    storedCredential = reader.IsDBNull(reader.GetOrdinal("PasswordHash"))
                        ? string.Empty
                        : reader.GetString(reader.GetOrdinal("PasswordHash"));
                    role = reader.IsDBNull(reader.GetOrdinal("Role"))
                        ? "Pet Owner"
                        : reader.GetString(reader.GetOrdinal("Role"));
                    mobileNumber = reader.IsDBNull(reader.GetOrdinal("MobileNumber"))
                        ? string.Empty
                        : reader.GetString(reader.GetOrdinal("MobileNumber"));
                }

                var user = new SharedAuthUser
                {
                    Id = userId,
                    FullName = fullName,
                    Email = email,
                    Role = role
                };

                bool valid = false;
                bool shouldUpgrade = false;

                // Final shared format: ASP.NET Identity PasswordHasher.
                try
                {
                    var verification =
                        _passwordHasher.VerifyHashedPassword(
                            user,
                            storedCredential,
                            request.Password);

                    valid = verification != PasswordVerificationResult.Failed;
                    shouldUpgrade =
                        verification == PasswordVerificationResult.SuccessRehashNeeded;
                }
                catch
                {
                    valid = false;
                }

                // Temporary migration support for accounts created by the
                // previous mobile API using BCrypt.
                if (!valid &&
                    (storedCredential.StartsWith("$2a$", StringComparison.Ordinal) ||
                     storedCredential.StartsWith("$2b$", StringComparison.Ordinal) ||
                     storedCredential.StartsWith("$2y$", StringComparison.Ordinal)))
                {
                    try
                    {
                        valid = BCrypt.Net.BCrypt.Verify(
                            request.Password,
                            storedCredential);
                        shouldUpgrade = valid;
                    }
                    catch
                    {
                        valid = false;
                    }
                }

                // Temporary migration support for legacy plaintext web rows.
                if (!valid &&
                    string.Equals(
                        storedCredential,
                        request.Password,
                        StringComparison.Ordinal))
                {
                    valid = true;
                    shouldUpgrade = true;
                }

                if (!valid)
                    return Unauthorized("Invalid email or password.");

                if (shouldUpgrade)
                {
                    string upgradedHash =
                        _passwordHasher.HashPassword(user, request.Password);

                    await using var upgradeCmd = new SqlCommand(@"
                        UPDATE UserAccounts
                        SET PasswordHash = @PasswordHash
                        WHERE Id = @Id;", connection);

                    upgradeCmd.Parameters.AddWithValue(
                        "@PasswordHash",
                        upgradedHash);
                    upgradeCmd.Parameters.AddWithValue("@Id", userId);

                    await upgradeCmd.ExecuteNonQueryAsync();
                }

                return Ok(new AuthResponse
                {
                    UserId = userId,
                    FullName = fullName,
                    Email = email,
                    Role = string.IsNullOrWhiteSpace(role)
                        ? "Pet Owner"
                        : role,
                    MobileNumber = mobileNumber,
                    ProfilePicture = string.Empty,
                    Token = Guid.NewGuid().ToString("N")
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Login error: {ex.Message}");
            }
        }
    }

    internal sealed class SharedAuthUser
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = "Pet Owner";
    }

    public sealed class RegisterRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public sealed class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public sealed class AuthResponse
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = "Pet Owner";
        public string MobileNumber { get; set; } = string.Empty;
        public string ProfilePicture { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
    }
}
