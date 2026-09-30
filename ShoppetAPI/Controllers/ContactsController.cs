using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ShoppetAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ContactsController : ControllerBase
{
    private readonly IConfiguration _configuration;
    public ContactsController(IConfiguration configuration) => _configuration = configuration;

    private string ConnectionString =>
        _configuration.GetConnectionString("SharedSqlServer")
        ?? throw new InvalidOperationException("SharedSqlServer connection string not found.");

    [HttpGet]
    public async Task<IActionResult> GetContacts([FromQuery] int? userId = null)
    {
        try
        {
            var contacts = new List<object>();
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            string sql = "SELECT Id, UserId, Name, Role, Address, Phone, IsEmergency FROM EmergencyContacts";
            if (userId.HasValue) sql += " WHERE UserId=@UserId";
            sql += " ORDER BY Name";

            await using var cmd = new SqlCommand(sql, connection);
            if (userId.HasValue) cmd.Parameters.AddWithValue("@UserId", userId.Value);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                contacts.Add(new
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    Name = reader.GetString(2),
                    Role = reader.GetString(3),
                    Address = reader.GetString(4),
                    Phone = reader.GetString(5),
                    IsEmergency = reader.GetBoolean(6)
                });
            }

            return Ok(contacts);
        }
        catch (Exception ex) { return StatusCode(500, $"Error fetching contacts: {ex.Message}"); }
    }

    [HttpPost]
    public async Task<IActionResult> CreateContact([FromBody] ContactRequest request)
    {
        if (request.UserId <= 0) return BadRequest("A valid user ID is required.");

        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var cmd = new SqlCommand(@"
                INSERT INTO EmergencyContacts(UserId, Name, Role, Address, Phone, IsEmergency)
                OUTPUT INSERTED.Id
                VALUES(@UserId,@Name,@Role,@Address,@Phone,@IsEmergency);", connection);

            AddParameters(cmd, request);
            int id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(new { Id = id, request.Name, request.Role, request.Phone });
        }
        catch (Exception ex) { return StatusCode(500, $"Error saving contact: {ex.Message}"); }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateContact(int id, [FromBody] ContactRequest request)
    {
        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var cmd = new SqlCommand(@"
                UPDATE EmergencyContacts
                SET Name=@Name, Role=@Role, Address=@Address, Phone=@Phone, IsEmergency=@IsEmergency
                WHERE Id=@Id AND UserId=@UserId;", connection);

            cmd.Parameters.AddWithValue("@Id", id);
            AddParameters(cmd, request);
            if (await cmd.ExecuteNonQueryAsync() == 0) return NotFound("Contact not found.");
            return Ok(new { Id = id, request.UserId, request.Name, request.Role, request.Address, request.Phone, request.IsEmergency });
        }
        catch (Exception ex) { return StatusCode(500, $"Error updating contact: {ex.Message}"); }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteContact(int id)
    {
        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var cmd = new SqlCommand("DELETE FROM EmergencyContacts WHERE Id=@Id", connection);
            cmd.Parameters.AddWithValue("@Id", id);
            return await cmd.ExecuteNonQueryAsync() > 0 ? Ok(new { message = "Contact deleted successfully" }) : NotFound("Contact not found.");
        }
        catch (Exception ex) { return StatusCode(500, $"Error deleting contact: {ex.Message}"); }
    }

    private static void AddParameters(SqlCommand cmd, ContactRequest request)
    {
        cmd.Parameters.AddWithValue("@UserId", request.UserId);
        cmd.Parameters.AddWithValue("@Name", request.Name ?? "");
        cmd.Parameters.AddWithValue("@Role", request.Role ?? "");
        cmd.Parameters.AddWithValue("@Address", request.Address ?? "");
        cmd.Parameters.AddWithValue("@Phone", request.Phone ?? "");
        cmd.Parameters.AddWithValue("@IsEmergency", request.IsEmergency);
    }
}

public class ContactRequest
{
    public int UserId { get; set; }
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public string Address { get; set; } = "";
    public string Phone { get; set; } = "";
    public bool IsEmergency { get; set; }
}
