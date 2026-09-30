using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using ShoppetAPI.Services;

namespace ShoppetAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public AuthController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private string ConnectionString =>
        _configuration.GetConnectionString("SharedSqlServer")
        ?? throw new InvalidOperationException("Connection string 'SharedSqlServer' was not found.");

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Full name, email, and password are required.");

        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            await using (var check = new SqlCommand(
                "SELECT COUNT(1) FROM UserAccounts WHERE Email=@Email",
                connection))
            {
                check.Parameters.AddWithValue("@Email", request.Email.Trim());
                var count = Convert.ToInt32(await check.ExecuteScalarAsync());
                if (count > 0)
                    return Conflict("Email is already registered.");
            }

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            const string insertSql = """
                INSERT INTO UserAccounts (FullName, Email, PasswordHash, Role, CreatedAt)
                OUTPUT INSERTED.Id
                VALUES (@FullName, @Email, @PasswordHash, 'Pet Owner', SYSDATETIME());
                """;

            await using var insert = new SqlCommand(insertSql, connection);
            insert.Parameters.AddWithValue("@FullName", request.FullName.Trim());
            insert.Parameters.AddWithValue("@Email", request.Email.Trim());
            insert.Parameters.AddWithValue("@PasswordHash", passwordHash);

            var userId = Convert.ToInt32(await insert.ExecuteScalarAsync());

            return Ok(new AuthResponse
            {
                UserId = userId,
                FullName = request.FullName.Trim(),
                Email = request.Email.Trim(),
                Role = "Pet Owner",
                Token = "local-session"
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
            return BadRequest("Email and password are required.");

        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            const string sql = """
                SELECT Id, FullName, Email, PasswordHash, Role
                FROM UserAccounts
                WHERE Email=@Email;
                """;

            await using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Email", request.Email.Trim());

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return Unauthorized("Invalid email or password.");

            var userId = reader.GetInt32(0);
            var fullName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            var email = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            var storedPassword = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
            var storedRole = reader.IsDBNull(4) ? "Pet Owner" : reader.GetString(4);

            var valid = VerifyPassword(request.Password, storedPassword);
            if (!valid)
                return Unauthorized("Invalid email or password.");

            var role = RbacService.NormalizeRole(storedRole);
            if (string.IsNullOrWhiteSpace(role))
                return StatusCode(403,
                    "This legacy account role is no longer part of the active ShoppetCare flow.");

            return Ok(new AuthResponse
            {
                UserId = userId,
                FullName = fullName,
                Email = email,
                Role = role,
                Token = "local-session"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Login error: {ex.Message}");
        }
    }

    private static bool VerifyPassword(string password, string storedPassword)
    {
        if (string.IsNullOrEmpty(storedPassword))
            return false;

        if (storedPassword.StartsWith("$2", StringComparison.Ordinal))
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(password, storedPassword);
            }
            catch
            {
                return false;
            }
        }

        // Compatibility for existing web accounts created before BCrypt migration.
        return string.Equals(password, storedPassword, StringComparison.Ordinal);
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
    public string Role { get; set; } = "Pet Owner";
    public string Token { get; set; } = string.Empty;
}
