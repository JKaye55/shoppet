using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.ComponentModel.DataAnnotations;
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
                string connString = _configuration.GetConnectionString("DefaultConnection")!;

                using (var connection = new MySqlConnection(connString))
                {
                    await connection.OpenAsync();

                    var checkCmd = new MySqlCommand("SELECT COUNT(*) FROM Users WHERE Email = @Email", connection);
                    checkCmd.Parameters.AddWithValue("@Email", request.Email);
                    long count = (long)await checkCmd.ExecuteScalarAsync();

                    if (count > 0)
                    {
                        return Conflict("Email is already registered.");
                    }

                    string hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

                    var insertCmd = new MySqlCommand(
                        "INSERT INTO Users (FullName, Email, PasswordHash) VALUES (@FullName, @Email, @PasswordHash); SELECT LAST_INSERT_ID();", connection);
                    insertCmd.Parameters.AddWithValue("@FullName", request.FullName);
                    insertCmd.Parameters.AddWithValue("@Email", request.Email);
                    insertCmd.Parameters.AddWithValue("@PasswordHash", hashedPassword);

                    int userId = Convert.ToInt32(await insertCmd.ExecuteScalarAsync());

                    return Ok(new AuthResponse
                    {
                        UserId = userId,
                        FullName = request.FullName,
                        Email = request.Email,
                        Role = "PetOwner",
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
                string connString = _configuration.GetConnectionString("DefaultConnection")!;

                using (var connection = new MySqlConnection(connString))
                {
                    await connection.OpenAsync();

                    var cmd = new MySqlCommand("SELECT Id, FullName, Email, PasswordHash FROM Users WHERE Email = @Email", connection);
                    cmd.Parameters.AddWithValue("@Email", request.Email);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            int userId = reader.GetInt32("Id");
                            string fullName = reader.GetString("FullName");
                            string email = reader.GetString("Email");
                            string passwordHash = reader.GetString("PasswordHash");

                            // Verify the entered password against the stored BCrypt hash
                            bool isValidPassword = BCrypt.Net.BCrypt.Verify(request.Password, passwordHash);

                            if (isValidPassword)
                            {
                                return Ok(new AuthResponse
                                {
                                    UserId = userId,
                                    FullName = fullName,
                                    Email = email,
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
        [Required]
        [MinLength(4)]
        public string FullName { get; set; } = string.Empty;
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;
    }

    public class LoginRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;
    }

    public class AuthResponse
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = "PetOwner";
        public string Token { get; set; } = string.Empty;
    }
}