using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ShoppetAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CommunityController : ControllerBase
{
    private readonly IConfiguration _configuration;
    public CommunityController(IConfiguration configuration) => _configuration = configuration;

    private string ConnectionString =>
        _configuration.GetConnectionString("SharedSqlServer")
        ?? throw new InvalidOperationException("SharedSqlServer connection string not found.");

    [HttpGet]
    public async Task<IActionResult> GetPosts([FromQuery] int userId = 0)
    {
        try
        {
            var posts = new List<object>();
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            await using var cmd = new SqlCommand(@"
                SELECT p.Id,p.UserId,p.PetId,p.Caption,p.ImageUrl,p.PostedAt,p.IsEdited,
                       u.FullName,u.ProfilePicture,pp.PetName,
                       (SELECT COUNT(*) FROM CommunityLikes cl WHERE cl.PostId=p.Id) AS LikesCount,
                       (SELECT COUNT(*) FROM CommunityComments cc WHERE cc.PostId=p.Id) AS CommentsCount,
                       CASE WHEN EXISTS(
                           SELECT 1 FROM CommunityLikes cl WHERE cl.PostId=p.Id AND cl.UserId=@UserId
                       ) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS IsLikedByMe
                FROM CommunityPosts p
                LEFT JOIN UserAccounts u ON u.Id=p.UserId
                LEFT JOIN PetProfiles pp ON pp.Id=p.PetId
                ORDER BY p.PostedAt DESC;", connection);
            cmd.Parameters.AddWithValue("@UserId", userId);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                posts.Add(new
                {
                    Id=reader.GetInt32(0),
                    UserId=reader.GetInt32(1),
                    PetId=reader.IsDBNull(2)?(int?)null:reader.GetInt32(2),
                    AuthorName=reader.IsDBNull(7)?"ShoppetCare User":reader.GetString(7),
                    ProfilePicture=reader.IsDBNull(8)?"":reader.GetString(8),
                    PetName=reader.IsDBNull(9)?"":reader.GetString(9),
                    Content=reader.IsDBNull(3)?"":reader.GetString(3),
                    ImageUrls=reader.IsDBNull(4)?"":reader.GetString(4),
                    Timestamp=reader.GetDateTime(5),
                    CreatedAt=reader.GetDateTime(5),
                    IsEdited=reader.GetBoolean(6),
                    LikesCount=reader.GetInt32(10),
                    CommentsCount=reader.GetInt32(11),
                    IsLikedByMe=reader.GetBoolean(12)
                });
            }
            return Ok(posts);
        }
        catch(Exception ex){ return StatusCode(500,$"Error fetching posts: {ex.Message}"); }
    }

    [HttpPost]
    public async Task<IActionResult> CreatePost([FromBody] CreatePostRequest request)
    {
        if(request.UserId<=0) return BadRequest("A valid user ID is required.");
        if(string.IsNullOrWhiteSpace(request.Content) && string.IsNullOrWhiteSpace(request.ImageUrls))
            return BadRequest("Post content or media is required.");

        try
        {
            await using var connection=new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var cmd=new SqlCommand(@"
                INSERT INTO CommunityPosts(UserId,PetId,Caption,ImageUrl,LikeCount,PostedAt,IsEdited)
                OUTPUT INSERTED.Id
                VALUES(@UserId,@PetId,@Content,@ImageUrls,0,SYSDATETIME(),0);",connection);
            cmd.Parameters.AddWithValue("@UserId",request.UserId);
            cmd.Parameters.AddWithValue("@PetId",(object?)request.PetId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Content",request.Content ?? "");
            cmd.Parameters.AddWithValue("@ImageUrls",(object?)request.ImageUrls ?? DBNull.Value);
            int id=Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(new{success=true,id});
        }
        catch(Exception ex){ return StatusCode(500,$"Error creating post: {ex.Message}"); }
    }

    [HttpPost("{postId:int}/like")]
    public async Task<IActionResult> ToggleLike(int postId,[FromBody] LikeRequest request)
    {
        try
        {
            await using var connection=new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var check=new SqlCommand("SELECT Id FROM CommunityLikes WHERE PostId=@PostId AND UserId=@UserId",connection);
            check.Parameters.AddWithValue("@PostId",postId);
            check.Parameters.AddWithValue("@UserId",request.UserId);
            var existing=await check.ExecuteScalarAsync();
            bool liked;
            if(existing is not null)
            {
                await using var del=new SqlCommand("DELETE FROM CommunityLikes WHERE PostId=@PostId AND UserId=@UserId",connection);
                del.Parameters.AddWithValue("@PostId",postId); del.Parameters.AddWithValue("@UserId",request.UserId);
                await del.ExecuteNonQueryAsync();
                liked=false;
            }
            else
            {
                await using var ins=new SqlCommand("INSERT INTO CommunityLikes(PostId,UserId,CreatedAt) VALUES(@PostId,@UserId,SYSDATETIME())",connection);
                ins.Parameters.AddWithValue("@PostId",postId); ins.Parameters.AddWithValue("@UserId",request.UserId);
                await ins.ExecuteNonQueryAsync();
                liked=true;
            }
            return Ok(new{success=true,isLiked=liked});
        }
        catch(Exception ex){ return StatusCode(500,$"Error toggling like: {ex.Message}"); }
    }

    [HttpGet("{postId:int}/comments")]
    public async Task<IActionResult> GetComments(int postId,[FromQuery] int userId=0)
    {
        try
        {
            var comments=new List<object>();
            await using var connection=new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var cmd=new SqlCommand(@"
                SELECT c.Id,c.PostId,c.UserId,c.ParentCommentId,c.Body,c.CreatedAt,
                       u.FullName,u.ProfilePicture,pu.FullName,
                       (SELECT COUNT(*) FROM CommunityCommentLikes l WHERE l.CommentId=c.Id) AS LikeCount,
                       CASE WHEN EXISTS(
                           SELECT 1 FROM CommunityCommentLikes l WHERE l.CommentId=c.Id AND l.UserId=@UserId
                       ) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS IsLikedByMe
                FROM CommunityComments c
                LEFT JOIN UserAccounts u ON u.Id=c.UserId
                LEFT JOIN CommunityComments pc ON pc.Id=c.ParentCommentId
                LEFT JOIN UserAccounts pu ON pu.Id=pc.UserId
                WHERE c.PostId=@PostId
                ORDER BY c.CreatedAt ASC;",connection);
            cmd.Parameters.AddWithValue("@PostId",postId);
            cmd.Parameters.AddWithValue("@UserId",userId);
            await using var reader=await cmd.ExecuteReaderAsync();
            while(await reader.ReadAsync())
            {
                comments.Add(new{
                    Id=reader.GetInt32(0),
                    PostId=reader.GetInt32(1),
                    UserId=reader.IsDBNull(2)?0:reader.GetInt32(2),
                    ParentCommentId=reader.IsDBNull(3)?(int?)null:reader.GetInt32(3),
                    AuthorName=reader.IsDBNull(6)?"Unknown":reader.GetString(6),
                    ProfilePicture=reader.IsDBNull(7)?"":reader.GetString(7),
                    ParentAuthorName=reader.IsDBNull(8)?null:reader.GetString(8),
                    Content=reader.GetString(4),
                    CreatedAt=reader.GetDateTime(5),
                    LikeCount=reader.GetInt32(9),
                    IsLikedByMe=reader.GetBoolean(10)
                });
            }
            return Ok(comments);
        }
        catch(Exception ex){ return StatusCode(500,$"Error fetching comments: {ex.Message}"); }
    }

    [HttpPost("{postId:int}/comments")]
    public async Task<IActionResult> AddComment(int postId,[FromBody] AddCommentRequest request)
    {
        if(request.UserId<=0 || string.IsNullOrWhiteSpace(request.Content)) return BadRequest("User and comment text are required.");
        try
        {
            await using var connection=new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var cmd=new SqlCommand(@"
                INSERT INTO CommunityComments(PostId,UserId,AuthorName,Body,IsGuest,CreatedAt,ParentCommentId)
                VALUES(@PostId,@UserId,'',@Content,0,SYSDATETIME(),@ParentCommentId);",connection);
            cmd.Parameters.AddWithValue("@PostId",postId);
            cmd.Parameters.AddWithValue("@UserId",request.UserId);
            cmd.Parameters.AddWithValue("@Content",request.Content);
            cmd.Parameters.AddWithValue("@ParentCommentId",(object?)request.ParentCommentId ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
            return Ok(new{success=true});
        }
        catch(Exception ex){ return StatusCode(500,$"Error adding comment: {ex.Message}"); }
    }

    [HttpPost("comments/{commentId:int}/like")]
    public async Task<IActionResult> ToggleCommentLike(int commentId,[FromBody] LikeRequest request)
    {
        try
        {
            await using var connection=new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var check=new SqlCommand("SELECT Id FROM CommunityCommentLikes WHERE CommentId=@CommentId AND UserId=@UserId",connection);
            check.Parameters.AddWithValue("@CommentId",commentId); check.Parameters.AddWithValue("@UserId",request.UserId);
            var existing=await check.ExecuteScalarAsync();
            bool liked;
            if(existing is not null)
            {
                await using var del=new SqlCommand("DELETE FROM CommunityCommentLikes WHERE CommentId=@CommentId AND UserId=@UserId",connection);
                del.Parameters.AddWithValue("@CommentId",commentId); del.Parameters.AddWithValue("@UserId",request.UserId);
                await del.ExecuteNonQueryAsync(); liked=false;
            }
            else
            {
                await using var ins=new SqlCommand("INSERT INTO CommunityCommentLikes(CommentId,UserId,CreatedAt) VALUES(@CommentId,@UserId,SYSDATETIME())",connection);
                ins.Parameters.AddWithValue("@CommentId",commentId); ins.Parameters.AddWithValue("@UserId",request.UserId);
                await ins.ExecuteNonQueryAsync(); liked=true;
            }
            return Ok(new{success=true,isLiked=liked});
        }
        catch(Exception ex){ return StatusCode(500,$"Error toggling comment like: {ex.Message}"); }
    }

    [HttpPut("{postId:int}")]
    public async Task<IActionResult> EditPost(int postId,[FromBody] EditPostRequest request)
    {
        try
        {
            await using var connection=new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var cmd=new SqlCommand(@"
                UPDATE CommunityPosts
                SET Caption=@Content,ImageUrl=@ImageUrls,PetId=@PetId,IsEdited=1
                WHERE Id=@Id;",connection);
            cmd.Parameters.AddWithValue("@Id",postId);
            cmd.Parameters.AddWithValue("@Content",request.Content ?? "");
            cmd.Parameters.AddWithValue("@ImageUrls",(object?)request.ImageUrls ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PetId",(object?)request.PetId ?? DBNull.Value);
            return await cmd.ExecuteNonQueryAsync()>0 ? Ok(new{success=true}) : NotFound();
        }
        catch(Exception ex){ return StatusCode(500,$"Error editing post: {ex.Message}"); }
    }

    [HttpDelete("comments/{commentId:int}")]
    public async Task<IActionResult> DeleteComment(int commentId)
    {
        try
        {
            await using var connection=new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            int? postId=null;
            await using(var pcmd=new SqlCommand("SELECT PostId FROM CommunityComments WHERE Id=@Id",connection))
            {
                pcmd.Parameters.AddWithValue("@Id",commentId);
                var val=await pcmd.ExecuteScalarAsync();
                if(val is null) return NotFound();
                postId=Convert.ToInt32(val);
            }

            var rows=new List<(int Id,int? ParentId)>();
            await using(var load=new SqlCommand("SELECT Id,ParentCommentId FROM CommunityComments WHERE PostId=@PostId",connection))
            {
                load.Parameters.AddWithValue("@PostId",postId.Value);
                await using var reader=await load.ExecuteReaderAsync();
                while(await reader.ReadAsync()) rows.Add((reader.GetInt32(0),reader.IsDBNull(1)?null:reader.GetInt32(1)));
            }

            var ids=new HashSet<int>{commentId};
            bool changed;
            do{
                changed=false;
                foreach(var row in rows)
                    if(row.ParentId.HasValue && ids.Contains(row.ParentId.Value) && ids.Add(row.Id)) changed=true;
            }while(changed);

            await using var tx=await connection.BeginTransactionAsync();
            foreach(int id in ids)
            {
                await using var likes=new SqlCommand("DELETE FROM CommunityCommentLikes WHERE CommentId=@Id",connection,(SqlTransaction)tx);
                likes.Parameters.AddWithValue("@Id",id);
                await likes.ExecuteNonQueryAsync();
            }
            foreach(int id in ids.OrderByDescending(x=>x))
            {
                await using var del=new SqlCommand("DELETE FROM CommunityComments WHERE Id=@Id",connection,(SqlTransaction)tx);
                del.Parameters.AddWithValue("@Id",id);
                await del.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();
            return Ok(new{success=true,deletedCount=ids.Count});
        }
        catch(Exception ex){ return StatusCode(500,$"Error deleting comment: {ex.Message}"); }
    }

    [HttpDelete("{postId:int}")]
    public async Task<IActionResult> DeletePost(int postId)
    {
        try
        {
            await using var connection=new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var tx=(SqlTransaction)await connection.BeginTransactionAsync();

            await using(var likes=new SqlCommand("DELETE FROM CommunityLikes WHERE PostId=@Id",connection,tx)){ likes.Parameters.AddWithValue("@Id",postId); await likes.ExecuteNonQueryAsync(); }
            await using(var clikes=new SqlCommand("DELETE FROM CommunityCommentLikes WHERE CommentId IN (SELECT Id FROM CommunityComments WHERE PostId=@Id)",connection,tx)){ clikes.Parameters.AddWithValue("@Id",postId); await clikes.ExecuteNonQueryAsync(); }
            await using(var comments=new SqlCommand("DELETE FROM CommunityComments WHERE PostId=@Id",connection,tx)){ comments.Parameters.AddWithValue("@Id",postId); await comments.ExecuteNonQueryAsync(); }
            int affected;
            await using(var post=new SqlCommand("DELETE FROM CommunityPosts WHERE Id=@Id",connection,tx)){ post.Parameters.AddWithValue("@Id",postId); affected=await post.ExecuteNonQueryAsync(); }
            await tx.CommitAsync();
            return affected>0 ? Ok(new{success=true}) : NotFound();
        }
        catch(Exception ex){ return StatusCode(500,$"Error deleting post: {ex.Message}"); }
    }
}

public class EditPostRequest
{
    public string Content { get; set; }="";
    public string? ImageUrls { get; set; }
    public int? PetId { get; set; }
    public string PetName { get; set; }="";
}
public class CreatePostRequest
{
    public int UserId { get; set; }
    public int? PetId { get; set; }
    public string? AuthorName { get; set; }
    public string? PetName { get; set; }
    public string? Content { get; set; }
    public string? ImageUrls { get; set; }
}
public class AddCommentRequest
{
    public int UserId { get; set; }
    public int? ParentCommentId { get; set; }
    public string Content { get; set; }="";
}
public class LikeRequest { public int UserId { get; set; } }
