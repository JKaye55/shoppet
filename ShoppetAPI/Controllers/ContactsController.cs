using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Data;

namespace ShoppetAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ContactsController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public ContactsController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> GetContacts([FromQuery] int? userId = null)
        {
            try
            {
                string connString = _configuration.GetConnectionString("DefaultConnection")!;
                var contacts = new List<object>();

                using (var connection = new MySqlConnection(connString))
                {
                    await connection.OpenAsync();
                    var query = "SELECT Id, UserId, Name, Role, Address, Phone, IsEmergency FROM emergencycontacts";

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
                            contacts.Add(new
                            {
                                Id = reader.GetInt32("Id"),
                                UserId = reader.GetInt32("UserId"),
                                Name = reader.GetString("Name"),
                                Role = reader.GetString("Role"),
                                Address = reader.GetString("Address"),
                                Phone = reader.GetString("Phone"),
                                IsEmergency = reader.GetBoolean("IsEmergency")
                            });
                        }
                    }
                }
            }
                return Ok(contacts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error fetching contacts: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateContact([FromBody] ContactRequest request)
        {
            try
            {
                if (request.UserId <= 0)
                    return BadRequest("A valid user ID is required.");

                string connString = _configuration.GetConnectionString("DefaultConnection")!;
                long newId = 0;

                using (var connection = new MySqlConnection(connString))
                {
                    await connection.OpenAsync();
                    var query = @"INSERT INTO emergencycontacts (UserId, Name, Role, Address, Phone, IsEmergency) 
                                  VALUES (@UserId, @Name, @Role, @Address, @Phone, @IsEmergency)";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@UserId", request.UserId);
                        cmd.Parameters.AddWithValue("@Name", request.Name ?? "");
                        cmd.Parameters.AddWithValue("@Role", request.Role ?? "");
                        cmd.Parameters.AddWithValue("@Address", request.Address ?? "");
                        cmd.Parameters.AddWithValue("@Phone", request.Phone ?? "");
                        cmd.Parameters.AddWithValue("@IsEmergency", request.IsEmergency);

                        await cmd.ExecuteNonQueryAsync();
                        newId = cmd.LastInsertedId;
                    }
                }

                return Ok(new { Id = (int)newId, request.Name, request.Role, request.Phone });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error saving contact: {ex.Message}");
            }
        }

            [HttpPut("{id}")]
        public async Task<IActionResult> UpdateContact(int id, [FromBody] ContactRequest request)
        {
            try
            {
                string connString = _configuration.GetConnectionString("DefaultConnection")!;
                using (var connection = new MySqlConnection(connString))
                {
                    await connection.OpenAsync();
                    var query = @"UPDATE emergencycontacts SET Name = @name, Role = @role, Address = @address, Phone = @phone, IsEmergency = @isEmergency WHERE Id = @id AND UserId = @userId";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.Parameters.AddWithValue("@userId", request.UserId);
                        cmd.Parameters.AddWithValue("@name", request.Name);
                        cmd.Parameters.AddWithValue("@role", request.Role);
                        cmd.Parameters.AddWithValue("@address", request.Address);
                        cmd.Parameters.AddWithValue("@phone", request.Phone);
                        cmd.Parameters.AddWithValue("@isEmergency", request.IsEmergency);

                        int rows = await cmd.ExecuteNonQueryAsync();
                        if (rows == 0) return NotFound("Contact not found.");

                        return Ok(new { Id = id, request.UserId, request.Name, request.Role, request.Address, request.Phone, request.IsEmergency });
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error updating contact: {ex.Message}");
            }
        }
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteContact(int id)
        {
            try
            {
                string connString = _configuration.GetConnectionString("DefaultConnection")!;

                using (var connection = new MySqlConnection(connString))
                {
                    await connection.OpenAsync();

                    var cmd = new MySqlCommand(
                        "DELETE FROM emergencycontacts WHERE Id = @Id",
                        connection);
                    cmd.Parameters.AddWithValue("@Id", id);

                    int rowsAffected = await cmd.ExecuteNonQueryAsync();

                    if (rowsAffected == 0)
                        return NotFound("Contact not found.");

                    return Ok(new { message = "Contact deleted successfully" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error deleting contact: {ex.Message}");
            }
        }

    }

    public class ContactRequest
    {
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public bool IsEmergency { get; set; }
    }
}