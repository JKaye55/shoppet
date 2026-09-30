using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace ShoppetAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessagesController : ControllerBase
{
    private readonly IConfiguration _config;
    public MessagesController(IConfiguration config) => _config = config;

    private string ConnectionString =>
        _config.GetConnectionString("SharedSqlServer")
        ?? throw new InvalidOperationException("SharedSqlServer connection string not found.");

    public class SendMessageRequest
    {
        public int SenderId { get; set; }
        public int ReceiverId { get; set; }
        public int? ListingId { get; set; }
        public string Text { get; set; } = "";
    }

    public class MessageItem
    {
        public int SenderId { get; set; }
        public int ReceiverId { get; set; }
        public int? ListingId { get; set; }
        public string Text { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public bool IsRead { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest req)
    {
        if (req.SenderId <= 0 || req.ReceiverId <= 0) return BadRequest("Valid sender and receiver IDs are required.");
        if (req.SenderId == req.ReceiverId) return BadRequest("You cannot message yourself.");
        if (string.IsNullOrWhiteSpace(req.Text)) return BadRequest("Message text is required.");

        try
        {
            int u1 = Math.Min(req.SenderId, req.ReceiverId);
            int u2 = Math.Max(req.SenderId, req.ReceiverId);
            await using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync();

            await using var select = new SqlCommand(
                "SELECT Id, MessagesJson, User1UnreadCount, User2UnreadCount FROM Conversations WHERE User1Id=@U1 AND User2Id=@U2",
                conn);
            select.Parameters.AddWithValue("@U1", u1);
            select.Parameters.AddWithValue("@U2", u2);

            int? id = null;
            string json = "[]";
            int unread1 = 0, unread2 = 0;
            await using (var reader = await select.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    id = reader.GetInt32(0);
                    json = reader.GetString(1);
                    unread1 = reader.GetInt32(2);
                    unread2 = reader.GetInt32(3);
                }
            }

            var messages = JsonSerializer.Deserialize<List<MessageItem>>(json) ?? [];
            messages.Add(new MessageItem
            {
                SenderId=req.SenderId,
                ReceiverId=req.ReceiverId,
                ListingId=req.ListingId,
                Text=req.Text.Trim(),
                Timestamp=DateTime.UtcNow,
                IsRead=false
            });

            if (req.ReceiverId == u1) unread1++; else unread2++;
            string updated = JsonSerializer.Serialize(messages);

            if (id.HasValue)
            {
                await using var update = new SqlCommand(@"
                    UPDATE Conversations
                    SET MessagesJson=@Json, LastUpdated=SYSUTCDATETIME(),
                        User1UnreadCount=@U1Unread, User2UnreadCount=@U2Unread
                    WHERE Id=@Id;", conn);
                update.Parameters.AddWithValue("@Json", updated);
                update.Parameters.AddWithValue("@U1Unread", unread1);
                update.Parameters.AddWithValue("@U2Unread", unread2);
                update.Parameters.AddWithValue("@Id", id.Value);
                await update.ExecuteNonQueryAsync();
            }
            else
            {
                await using var insert = new SqlCommand(@"
                    INSERT INTO Conversations(User1Id,User2Id,MessagesJson,LastUpdated,User1UnreadCount,User2UnreadCount)
                    VALUES(@U1,@U2,@Json,SYSUTCDATETIME(),@U1Unread,@U2Unread);", conn);
                insert.Parameters.AddWithValue("@U1", u1);
                insert.Parameters.AddWithValue("@U2", u2);
                insert.Parameters.AddWithValue("@Json", updated);
                insert.Parameters.AddWithValue("@U1Unread", unread1);
                insert.Parameters.AddWithValue("@U2Unread", unread2);
                await insert.ExecuteNonQueryAsync();
            }

            return Ok(new { success = true });
        }
        catch (Exception ex) { return StatusCode(500, ex.Message); }
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchUsers([FromQuery] string query, [FromQuery] int currentUserId)
    {
        if (string.IsNullOrWhiteSpace(query)) return Ok(new List<object>());
        try
        {
            var result = new List<object>();
            await using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(@"
                SELECT TOP 20 Id, FullName, Email, ProfilePicture
                FROM UserAccounts
                WHERE FullName LIKE @Query AND Id<>@CurrentUserId
                ORDER BY FullName;", conn);
            cmd.Parameters.AddWithValue("@Query", $"%{query}%");
            cmd.Parameters.AddWithValue("@CurrentUserId", currentUserId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new
                {
                    UserId=reader.GetInt32(0),
                    FullName=reader.GetString(1),
                    Email=reader.GetString(2),
                    ProfilePicture=reader.IsDBNull(3) ? "" : reader.GetString(3)
                });
            }
            return Ok(result);
        }
        catch (Exception ex) { return StatusCode(500, ex.Message); }
    }

    [HttpGet("{userId:int}")]
    public async Task<IActionResult> GetConversations(int userId)
    {
        if (userId <= 0) return BadRequest("A valid user ID is required.");
        try
        {
            var result = new List<object>();
            await using var conn = new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(@"
                SELECT c.User1Id,c.User2Id,c.MessagesJson,c.LastUpdated,c.User1UnreadCount,c.User2UnreadCount,
                       u.Id,u.FullName,u.ProfilePicture
                FROM Conversations c
                JOIN UserAccounts u
                  ON u.Id=CASE WHEN c.User1Id=@UserId THEN c.User2Id ELSE c.User1Id END
                WHERE c.User1Id=@UserId OR c.User2Id=@UserId
                ORDER BY c.LastUpdated DESC;", conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                int u1=reader.GetInt32(0), u2=reader.GetInt32(1);
                var msgs=JsonSerializer.Deserialize<List<MessageItem>>(reader.GetString(2)) ?? [];
                var last=msgs.LastOrDefault();
                result.Add(new
                {
                    ContactId=reader.GetInt32(6),
                    ContactName=reader.GetString(7),
                    ProfilePicture=reader.IsDBNull(8) ? "" : reader.GetString(8),
                    LastMessage=last?.Text ?? "",
                    Timestamp=reader.GetDateTime(3),
                    UnreadCount=userId==u1 ? reader.GetInt32(4) : reader.GetInt32(5)
                });
            }
            return Ok(result);
        }
        catch (Exception ex) { return StatusCode(500, ex.Message); }
    }

    [HttpGet("chat/{userId:int}/{contactId:int}")]
    public async Task<IActionResult> GetMessages(int userId, int contactId)
    {
        try
        {
            int u1=Math.Min(userId,contactId), u2=Math.Max(userId,contactId);
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            await using var cmd=new SqlCommand("SELECT MessagesJson FROM Conversations WHERE User1Id=@U1 AND User2Id=@U2",conn);
            cmd.Parameters.AddWithValue("@U1",u1); cmd.Parameters.AddWithValue("@U2",u2);
            var scalar=await cmd.ExecuteScalarAsync();
            if (scalar is null || scalar==DBNull.Value) return Ok(new List<MessageItem>());
            return Ok(JsonSerializer.Deserialize<List<MessageItem>>(Convert.ToString(scalar)!) ?? []);
        }
        catch(Exception ex){ return StatusCode(500,ex.Message); }
    }

    [HttpGet("unreadCount/{userId:int}")]
    public async Task<IActionResult> GetUnreadCount(int userId)
    {
        try
        {
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            await using var cmd=new SqlCommand(@"
                SELECT COALESCE(SUM(CASE WHEN User1Id=@UserId THEN User1UnreadCount ELSE User2UnreadCount END),0)
                FROM Conversations
                WHERE User1Id=@UserId OR User2Id=@UserId;",conn);
            cmd.Parameters.AddWithValue("@UserId",userId);
            return Ok(Convert.ToInt32(await cmd.ExecuteScalarAsync()));
        }
        catch(Exception ex){ return StatusCode(500,ex.Message); }
    }

    [HttpPost("resetUnread/{userId:int}/{contactId:int}")]
    public async Task<IActionResult> ResetUnread(int userId,int contactId)
    {
        try
        {
            int u1=Math.Min(userId,contactId),u2=Math.Max(userId,contactId);
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();

            await using var sel=new SqlCommand("SELECT Id,MessagesJson FROM Conversations WHERE User1Id=@U1 AND User2Id=@U2",conn);
            sel.Parameters.AddWithValue("@U1",u1); sel.Parameters.AddWithValue("@U2",u2);
            int? id=null; string json="[]";
            await using(var reader=await sel.ExecuteReaderAsync())
            {
                if(await reader.ReadAsync()){ id=reader.GetInt32(0); json=reader.GetString(1); }
            }
            if(!id.HasValue) return Ok();

            var msgs=JsonSerializer.Deserialize<List<MessageItem>>(json) ?? [];
            foreach(var m in msgs) if(m.SenderId==contactId) m.IsRead=true;

            await using var cmd=new SqlCommand(@"
                UPDATE Conversations
                SET MessagesJson=@Json,
                    User1UnreadCount=CASE WHEN User1Id=@UserId THEN 0 ELSE User1UnreadCount END,
                    User2UnreadCount=CASE WHEN User2Id=@UserId THEN 0 ELSE User2UnreadCount END
                WHERE Id=@Id;",conn);
            cmd.Parameters.AddWithValue("@Json",JsonSerializer.Serialize(msgs));
            cmd.Parameters.AddWithValue("@UserId",userId);
            cmd.Parameters.AddWithValue("@Id",id.Value);
            await cmd.ExecuteNonQueryAsync();
            return Ok();
        }
        catch(Exception ex){ return StatusCode(500,ex.Message); }
    }
}
