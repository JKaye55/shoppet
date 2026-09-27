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
                    INSERT INTO conversations (User1Id, User2Id, MessagesJSON, LastUpdated)
                    VALUES (@User1, @User2, CONCAT('[', @JsonMessage, ']'), NOW())
                    ON DUPLICATE KEY UPDATE 
                        MessagesJSON = JSON_ARRAY_APPEND(MessagesJSON, '$', JSON_EXTRACT(@JsonMessage, '$')),
                        LastUpdated = NOW()";
                        
                using var cmd = new MySqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@User1", user1);
                cmd.Parameters.AddWithValue("@User2", user2);
                cmd.Parameters.AddWithValue("@JsonMessage", jsonMessage);
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
                var sql = "SELECT Id, FullName, Email FROM users WHERE FullName LIKE @Query AND Id != @CurrentUserId LIMIT 20";
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
                        Email = reader["Email"].ToString()
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
                        u.FullName AS ContactName,
                        c.MessagesJSON,
                        c.LastUpdated
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
                        LastMessage = last?.Text ?? "",
                        Timestamp = Convert.ToDateTime(reader["LastUpdated"])
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
    }
}


