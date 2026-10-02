using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ShoppetAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly IConfiguration _configuration;
    public NotificationsController(IConfiguration configuration) => _configuration = configuration;

    private string ConnectionString =>
        _configuration.GetConnectionString("SharedSqlServer")
        ?? throw new InvalidOperationException("SharedSqlServer connection is missing.");

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int userId)
    {
        if (userId <= 0) return BadRequest("A valid user is required.");
        var list = new List<object>();
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand("""
            SELECT TOP 50 Id,UserId,ISNULL(Title,''),ISNULL(Body,''),
                   ISNULL(Link,''),ISNULL(Icon,''),ISNULL(IsRead,0),CreatedAt
            FROM Notifications
            WHERE UserId=@UserId
            ORDER BY CreatedAt DESC;
            """, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new {
                Id=r.GetInt32(0), UserId=r.GetInt32(1), Title=r.GetString(2),
                Body=r.GetString(3), Link=r.GetString(4), Icon=r.GetString(5),
                IsRead=r.GetBoolean(6), CreatedAt=r.GetDateTime(7)
            });
        }
        return Ok(list);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount([FromQuery] int userId)
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(
            "SELECT COUNT(*) FROM Notifications WHERE UserId=@UserId AND IsRead=0", conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        return Ok(Convert.ToInt32(await cmd.ExecuteScalarAsync()));
    }

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id,[FromQuery] int userId)
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(
            "UPDATE Notifications SET IsRead=1 WHERE Id=@Id AND UserId=@UserId", conn);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@UserId", userId);
        return await cmd.ExecuteNonQueryAsync()==0 ? NotFound() : Ok(new{success=true});
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead([FromQuery] int userId)
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(
            "UPDATE Notifications SET IsRead=1 WHERE UserId=@UserId AND IsRead=0", conn);
        cmd.Parameters.AddWithValue("@UserId", userId);
        await cmd.ExecuteNonQueryAsync();
        return Ok(new{success=true});
    }
}
