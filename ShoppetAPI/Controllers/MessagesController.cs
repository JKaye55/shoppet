using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Text.Json;

namespace ShoppetAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MessagesController : ControllerBase
    {
        private readonly IConfiguration _config;
        public MessagesController(IConfiguration config)
        {
            _config = config;
        }

        public class SendMessageRequest
        {
            public int SenderId { get; set; }
            public int ReceiverId { get; set; }
            public int? ListingId { get; set; }
            public string Text { get; set; } = string.Empty;
        }
        
        public class MessageItem
        {
            public int SenderId { get; set; }
            public int ReceiverId { get; set; }
            public int? ListingId { get; set; }
            public string Text { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; }
            public bool IsRead { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest req)
        {
            try
            {
                var msg = new MessageItem
                {
                    SenderId = req.SenderId,
                    ReceiverId = req.ReceiverId,
                    ListingId = req.ListingId,
                    Text = req.Text,
                    Timestamp = DateTime.UtcNow
                };
                
                string jsonMessage = JsonSerializer.Serialize(msg);
                int user1 = Math.Min(req.SenderId, req.ReceiverId);
                int user2 = Math.Max(req.SenderId, req.ReceiverId);

                using var conn = new MySqlConnection(_config.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();
                
                var query = @"
                    INSERT INTO conversations (User1Id, User2Id, MessagesJSON, LastUpdated, User1UnreadCount, User2UnreadCount)
                    VALUES (@User1, @User2, CONCAT('[', @JsonMessage, ']'), NOW(), @InitU1Unread, @InitU2Unread)
                    ON DUPLICATE KEY UPDATE 
                        MessagesJSON = JSON_ARRAY_APPEND(MessagesJSON, '$', JSON_EXTRACT(@JsonMessage, '$')),
                        LastUpdated = NOW(),
                        User1UnreadCount = User1UnreadCount + @IncU1Unread,
                        User2UnreadCount = User2UnreadCount + @IncU2Unread";
                        
                using var cmd = new MySqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@User1", user1);
                cmd.Parameters.AddWithValue("@User2", user2);
                cmd.Parameters.AddWithValue("@JsonMessage", jsonMessage);
                
                int initU1 = req.SenderId == user2 ? 1 : 0;
                int initU2 = req.SenderId == user1 ? 1 : 0;
                cmd.Parameters.AddWithValue("@InitU1Unread", initU1);
                cmd.Parameters.AddWithValue("@InitU2Unread", initU2);
                cmd.Parameters.AddWithValue("@IncU1Unread", initU1);
                cmd.Parameters.AddWithValue("@IncU2Unread", initU2);
                
                await cmd.ExecuteNonQueryAsync();
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
                using var conn = new MySqlConnection(_config.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();
                var sql = "SELECT Id, FullName, Email, ProfilePicture FROM users WHERE FullName LIKE @Query AND Id != @CurrentUserId LIMIT 20";
                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Query", $"%{query}%");
                cmd.Parameters.AddWithValue("@CurrentUserId", currentUserId);
                using var reader = await cmd.ExecuteReaderAsync();
                var result = new List<object>();
                while (await reader.ReadAsync())
                {
                    result.Add(new {
                        UserId = Convert.ToInt32(reader["Id"]),
                        FullName = reader["FullName"].ToString(),
                        Email = reader["Email"].ToString(),
                        ProfilePicture = reader["ProfilePicture"] == DBNull.Value ? string.Empty : reader["ProfilePicture"].ToString()
                    });
                }
                return Ok(result);
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetConversations(int userId)
        {
            try
            {
                using var conn = new MySqlConnection(_config.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();
                var query = @"
                    SELECT 
                        c.Id AS ConversationId,
                        CASE WHEN c.User1Id = @UserId THEN c.User2Id ELSE c.User1Id END AS ContactId,
                        u.FullName AS ContactName, u.ProfilePicture,
                        c.MessagesJSON,
                        c.LastUpdated,
                        CASE WHEN c.User1Id = @UserId THEN c.User1UnreadCount ELSE c.User2UnreadCount END AS UnreadCount
                    FROM conversations c
                    JOIN users u ON u.Id = (CASE WHEN c.User1Id = @UserId THEN c.User2Id ELSE c.User1Id END)
                    WHERE c.User1Id = @UserId OR c.User2Id = @UserId
                    ORDER BY c.LastUpdated DESC";
                
                using var cmd = new MySqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@UserId", userId);
                using var reader = await cmd.ExecuteReaderAsync();
                var result = new List<object>();
                while (await reader.ReadAsync())
                {
                    var msgsJson = reader["MessagesJSON"].ToString();
                    List<MessageItem>? msgs = null;
                    if (!string.IsNullOrEmpty(msgsJson))
                    {
                        try { msgs = JsonSerializer.Deserialize<List<MessageItem>>(msgsJson); }
                        catch { }
                    }
                    var last = msgs?.LastOrDefault();
                    
                    result.Add(new {
                        ContactId = Convert.ToInt32(reader["ContactId"]),
                        ContactName = reader["ContactName"].ToString(),
                        ProfilePicture = reader["ProfilePicture"] == DBNull.Value ? string.Empty : reader["ProfilePicture"].ToString(),
                        LastMessage = last?.Text ?? "",
                        Timestamp = Convert.ToDateTime(reader["LastUpdated"]),
                        UnreadCount = Convert.ToInt32(reader["UnreadCount"])
                    });
                }
                return Ok(result);
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        [HttpGet("chat/{userId}/{contactId}")]
        public async Task<IActionResult> GetMessages(int userId, int contactId)
        {
            try
            {
                int user1 = Math.Min(userId, contactId);
                int user2 = Math.Max(userId, contactId);
                
                using var conn = new MySqlConnection(_config.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();
                var query = "SELECT MessagesJSON FROM conversations WHERE User1Id = @User1 AND User2Id = @User2";
                
                using var cmd = new MySqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@User1", user1);
                cmd.Parameters.AddWithValue("@User2", user2);
                var scalar = await cmd.ExecuteScalarAsync();
                
                if (scalar == null || scalar == DBNull.Value) return Ok(new List<object>());
                
                var msgsJson = scalar.ToString();
                List<MessageItem>? msgs = null;
                if (!string.IsNullOrEmpty(msgsJson))
                {
                    try { msgs = JsonSerializer.Deserialize<List<MessageItem>>(msgsJson); }
                    catch { }
                }
                
                return Ok(msgs ?? new List<MessageItem>());
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        [HttpGet("unreadCount/{userId}")]
        public async Task<IActionResult> GetUnreadCount(int userId)
        {
            try
            {
                using var conn = new MySqlConnection(_config.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();
                var query = "SELECT COALESCE(SUM(CASE WHEN User1Id = @UserId THEN User1UnreadCount ELSE User2UnreadCount END), 0) FROM conversations WHERE User1Id = @UserId OR User2Id = @UserId";
                using var cmd = new MySqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@UserId", userId);
                var scalar = await cmd.ExecuteScalarAsync();
                return Ok(Convert.ToInt32(scalar));
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        [HttpPost("resetUnread/{userId}/{contactId}")]
        public async Task<IActionResult> ResetUnread(int userId, int contactId)
        {
            try
            {
                int user1 = Math.Min(userId, contactId);
                int user2 = Math.Max(userId, contactId);
                using var conn = new MySqlConnection(_config.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();
                
                // First read the existing messages to update IsRead status
                var selectQuery = "SELECT MessagesJSON FROM conversations WHERE User1Id = @User1 AND User2Id = @User2";
                using var selectCmd = new MySqlCommand(selectQuery, conn);
                selectCmd.Parameters.AddWithValue("@User1", user1);
                selectCmd.Parameters.AddWithValue("@User2", user2);
                
                var messagesJson = await selectCmd.ExecuteScalarAsync() as string;
                if (!string.IsNullOrEmpty(messagesJson))
                {
                    var messages = System.Text.Json.JsonSerializer.Deserialize<List<MessageItem>>(messagesJson) ?? new List<MessageItem>();
                    bool needsUpdate = false;
                    foreach (var msg in messages)
                    {
                        // If it's sent by the other person (contactId) and not read, mark it as read
                        if (msg.SenderId == contactId && !msg.IsRead)
                        {
                            msg.IsRead = true;
                            needsUpdate = true;
                        }
                    }
                    
                    if (needsUpdate)
                    {
                        var updatedJson = System.Text.Json.JsonSerializer.Serialize(messages);
                        var updateJsonQuery = "UPDATE conversations SET MessagesJSON = @Messages WHERE User1Id = @User1 AND User2Id = @User2";
                        using var updateJsonCmd = new MySqlCommand(updateJsonQuery, conn);
                        updateJsonCmd.Parameters.AddWithValue("@Messages", updatedJson);
                        updateJsonCmd.Parameters.AddWithValue("@User1", user1);
                        updateJsonCmd.Parameters.AddWithValue("@User2", user2);
                        await updateJsonCmd.ExecuteNonQueryAsync();
                    }
                }

                // user1=Min(userId,contactId), user2=Max. Check both columns against @UserId
                var query = @"
                    UPDATE conversations 
                    SET 
                        User1UnreadCount = CASE WHEN @UserId = User1Id THEN 0 ELSE User1UnreadCount END,
                        User2UnreadCount = CASE WHEN @UserId = User2Id THEN 0 ELSE User2UnreadCount END
                    WHERE User1Id = @User1 AND User2Id = @User2";
                using var cmd = new MySqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@User1", user1);
                cmd.Parameters.AddWithValue("@User2", user2);
                await cmd.ExecuteNonQueryAsync();
                return Ok();
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }
    }
}


