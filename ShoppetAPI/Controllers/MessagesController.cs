using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ShoppetAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessagesController : ControllerBase
{
    private readonly IConfiguration _config;
    public MessagesController(IConfiguration config)=>_config=config;

    private string ConnectionString =>
        _config.GetConnectionString("SharedSqlServer")
        ?? throw new InvalidOperationException("SharedSqlServer connection is missing.");

    public class SendMessageRequest
    {
        public int SenderId{get;set;}
        public int ReceiverId{get;set;}
        public int? ListingId{get;set;}
        public string Text{get;set;}=string.Empty;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string query,[FromQuery] int currentUserId=0)
    {
        try
        {
            var list=new List<object>();
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            await using var cmd=new SqlCommand("""
                SELECT TOP 20 Id,FullName,Email,ISNULL(ProfilePicture,'')
                FROM UserAccounts
                WHERE Id<>@CurrentUserId AND Role IN('Pet Owner','PetOwner')
                  AND (FullName LIKE @Query OR Email LIKE @Query)
                ORDER BY FullName;
                """,conn);
            cmd.Parameters.AddWithValue("@CurrentUserId",currentUserId);
            cmd.Parameters.AddWithValue("@Query",$"%{query?.Trim()??string.Empty}%");
            await using var r=await cmd.ExecuteReaderAsync();
            while(await r.ReadAsync())
                list.Add(new{UserId=r.GetInt32(0),FullName=r.GetString(1),Email=r.GetString(2),ProfilePicture=r.GetString(3)});
            return Ok(list);
        }
        catch(Exception ex){return StatusCode(500,$"User search error: {ex.Message}");}
    }

    [HttpPost]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest req)
    {
        if(req.SenderId<=0||req.ReceiverId<=0||req.SenderId==req.ReceiverId||string.IsNullOrWhiteSpace(req.Text)||req.Text.Length>1000)
            return BadRequest("Sender, receiver, and message are required.");
        try
        {
            int u1=Math.Min(req.SenderId,req.ReceiverId);
            int u2=Math.Max(req.SenderId,req.ReceiverId);

            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            if(!await ShoppetAPI.Services.RbacService.IsPetOwnerAsync(conn,req.ReceiverId))return BadRequest("Choose another Pet Owner as the recipient.");
            await using var tx=(SqlTransaction)await conn.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            int conversationId;
            await using(var get=new SqlCommand(
                "SELECT Id FROM Conversations WITH(UPDLOCK,HOLDLOCK) WHERE User1Id=@U1 AND User2Id=@U2",conn,tx))
            {
                get.Parameters.AddWithValue("@U1",u1);
                get.Parameters.AddWithValue("@U2",u2);
                var existing=await get.ExecuteScalarAsync();
                if(existing is null)
                {
                    await using var create=new SqlCommand("""
                        INSERT INTO Conversations(User1Id,User2Id,LastUpdated,User1UnreadCount,User2UnreadCount)
                        OUTPUT INSERTED.Id
                        VALUES(@U1,@U2,SYSDATETIME(),0,0);
                        """,conn,tx);
                    create.Parameters.AddWithValue("@U1",u1);
                    create.Parameters.AddWithValue("@U2",u2);
                    conversationId=Convert.ToInt32(await create.ExecuteScalarAsync());
                }
                else conversationId=Convert.ToInt32(existing);
            }

            int messageId;
            await using(var ins=new SqlCommand("""
                INSERT INTO Messages(ConversationId,SenderId,ReceiverId,ListingId,[Text],[Timestamp],IsRead)
                OUTPUT INSERTED.Id
                VALUES(@ConversationId,@SenderId,@ReceiverId,@ListingId,@Text,SYSDATETIME(),0);
                """,conn,tx))
            {
                ins.Parameters.AddWithValue("@ConversationId",conversationId);
                ins.Parameters.AddWithValue("@SenderId",req.SenderId);
                ins.Parameters.AddWithValue("@ReceiverId",req.ReceiverId);
                ins.Parameters.AddWithValue("@ListingId",req.ListingId.HasValue?req.ListingId.Value:DBNull.Value);
                ins.Parameters.AddWithValue("@Text",req.Text.Trim());
                messageId=Convert.ToInt32(await ins.ExecuteScalarAsync());
            }

            await using(var update=new SqlCommand("""
                UPDATE Conversations
                SET LastUpdated=SYSDATETIME(),
                    User1UnreadCount=User1UnreadCount + CASE WHEN User1Id=@ReceiverId THEN 1 ELSE 0 END,
                    User2UnreadCount=User2UnreadCount + CASE WHEN User2Id=@ReceiverId THEN 1 ELSE 0 END
                WHERE Id=@ConversationId;
                """,conn,tx))
            {
                update.Parameters.AddWithValue("@ReceiverId",req.ReceiverId);
                update.Parameters.AddWithValue("@ConversationId",conversationId);
                await update.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            return Ok(new{success=true,id=messageId});
        }
        catch(Exception ex){return StatusCode(500,$"Message send error: {ex.Message}");}
    }

    [HttpGet("{userId:int}")]
    public async Task<IActionResult> GetConversations(int userId)
    {
        try
        {
            var list=new List<object>();
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            const string sql="""
                SELECT c.Id,
                       CASE WHEN c.User1Id=@UserId THEN c.User2Id ELSE c.User1Id END ContactId,
                       ISNULL(u.FullName,'Unknown') ContactName,
                       ISNULL(u.ProfilePicture,'') ProfilePicture,
                       ISNULL((
                           SELECT TOP 1 m.[Text]
                           FROM Messages m
                           WHERE m.ConversationId=c.Id
                           ORDER BY m.[Timestamp] DESC,m.Id DESC
                       ),'') LastMessage,
                       c.LastUpdated,
                       CASE WHEN c.User1Id=@UserId THEN c.User1UnreadCount ELSE c.User2UnreadCount END UnreadCount
                FROM Conversations c
                JOIN UserAccounts u
                  ON u.Id=CASE WHEN c.User1Id=@UserId THEN c.User2Id ELSE c.User1Id END
                WHERE c.User1Id=@UserId OR c.User2Id=@UserId
                ORDER BY c.LastUpdated DESC;
                """;
            await using var cmd=new SqlCommand(sql,conn);
            cmd.Parameters.AddWithValue("@UserId",userId);
            await using var r=await cmd.ExecuteReaderAsync();
            while(await r.ReadAsync())
                list.Add(new{
                    ContactId=r.GetInt32(1),ContactName=r.GetString(2),ProfilePicture=r.GetString(3),
                    LastMessage=r.GetString(4),Timestamp=r.GetDateTime(5),UnreadCount=r.GetInt32(6)
                });
            return Ok(list);
        }
        catch(Exception ex){return StatusCode(500,$"Conversations error: {ex.Message}");}
    }

    [HttpGet("chat/{userId:int}/{contactId:int}")]
    public async Task<IActionResult> GetMessages(int userId,int contactId)
    {
        try
        {
            int u1=Math.Min(userId,contactId),u2=Math.Max(userId,contactId);
            var list=new List<object>();
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            const string sql="""
                SELECT m.Id,m.SenderId,m.ReceiverId,m.[Text],m.[Timestamp],m.IsRead
                FROM Messages m
                JOIN Conversations c ON c.Id=m.ConversationId
                WHERE c.User1Id=@U1 AND c.User2Id=@U2
                ORDER BY m.[Timestamp],m.Id;
                """;
            await using var cmd=new SqlCommand(sql,conn);
            cmd.Parameters.AddWithValue("@U1",u1);
            cmd.Parameters.AddWithValue("@U2",u2);
            await using var r=await cmd.ExecuteReaderAsync();
            while(await r.ReadAsync())
                list.Add(new{
                    Id=r.GetInt32(0),SenderId=r.GetInt32(1),ReceiverId=r.GetInt32(2),
                    Text=r.GetString(3),Timestamp=r.GetDateTime(4),IsRead=r.GetBoolean(5)
                });
            return Ok(list);
        }
        catch(Exception ex){return StatusCode(500,$"Messages error: {ex.Message}");}
    }

    [HttpGet("unreadCount/{userId:int}")]
    public async Task<IActionResult> GetUnreadCount(int userId)
    {
        try
        {
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            await using var cmd=new SqlCommand("""
                SELECT ISNULL(SUM(CASE WHEN User1Id=@UserId THEN User1UnreadCount ELSE User2UnreadCount END),0)
                FROM Conversations
                WHERE User1Id=@UserId OR User2Id=@UserId;
                """,conn);
            cmd.Parameters.AddWithValue("@UserId",userId);
            return Ok(Convert.ToInt32(await cmd.ExecuteScalarAsync()));
        }
        catch(Exception ex){return StatusCode(500,$"Unread count error: {ex.Message}");}
    }

    [HttpPost("resetUnread/{userId:int}/{contactId:int}")]
    public async Task<IActionResult> ResetUnread(int userId,int contactId)
    {
        try
        {
            int u1=Math.Min(userId,contactId),u2=Math.Max(userId,contactId);
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            await using var tx=(SqlTransaction)await conn.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            await using(var read=new SqlCommand("""
                UPDATE m SET IsRead=1
                FROM Messages m
                JOIN Conversations c ON c.Id=m.ConversationId
                WHERE c.User1Id=@U1 AND c.User2Id=@U2
                  AND m.ReceiverId=@UserId;
                """,conn,tx))
            {
                read.Parameters.AddWithValue("@U1",u1);
                read.Parameters.AddWithValue("@U2",u2);
                read.Parameters.AddWithValue("@UserId",userId);
                await read.ExecuteNonQueryAsync();
            }

            await using(var reset=new SqlCommand("""
                UPDATE Conversations
                SET User1UnreadCount=CASE WHEN User1Id=@UserId THEN 0 ELSE User1UnreadCount END,
                    User2UnreadCount=CASE WHEN User2Id=@UserId THEN 0 ELSE User2UnreadCount END
                WHERE User1Id=@U1 AND User2Id=@U2;
                """,conn,tx))
            {
                reset.Parameters.AddWithValue("@U1",u1);
                reset.Parameters.AddWithValue("@U2",u2);
                reset.Parameters.AddWithValue("@UserId",userId);
                await reset.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();
            return Ok(new{success=true});
        }
        catch(Exception ex){return StatusCode(500,$"Unread reset error: {ex.Message}");}
    }
}
