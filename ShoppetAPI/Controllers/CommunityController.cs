using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace ShoppetAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CommunityController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        public CommunityController(IConfiguration configuration) => _configuration = configuration;

        [HttpGet]
        public async Task<IActionResult> GetPosts([FromQuery] int userId = 0)
        {
            try
            {
                string conn = _configuration.GetConnectionString("DefaultConnection")!;
                var posts = new List<object>();
                using var connection = new MySqlConnection(conn);
                await connection.OpenAsync();
                
                var query = @"
                    SELECT p.Id, p.UserId, p.PetId, p.AuthorName AS OriginalAuthorName, p.PetName, p.Content, p.ImageUrl, p.Timestamp, p.IsEdited,
                           u.FullName AS UserFullName,
                           (SELECT COUNT(*) FROM communitylikes cl WHERE cl.PostId = p.Id) AS LikesCount,
                           (SELECT COUNT(*) FROM communitycomments cc WHERE cc.PostId = p.Id) AS CommentsCount,
                           EXISTS(SELECT 1 FROM communitylikes cl WHERE cl.PostId = p.Id AND cl.UserId = @UserId) AS IsLikedByMe
                    FROM communityposts p
                    LEFT JOIN users u ON p.UserId = u.Id
                    ORDER BY p.Timestamp DESC;";
                    
                using var cmd = new MySqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@UserId", userId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    posts.Add(new {
                        Id = Convert.ToInt32(reader["Id"]),
                        UserId = Convert.ToInt32(reader["UserId"]),
                        PetId = reader["PetId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["PetId"]),
                        AuthorName = reader["UserFullName"] == DBNull.Value ? reader["OriginalAuthorName"].ToString() : reader["UserFullName"].ToString(),
                        PetName = reader["PetName"] == DBNull.Value ? "" : reader["PetName"].ToString(),
                        Content = reader["Content"].ToString(),
                        ImageUrls = reader["ImageUrl"] == DBNull.Value ? "" : reader["ImageUrl"].ToString(),
                        Timestamp = Convert.ToDateTime(reader["Timestamp"]),
                        CreatedAt = Convert.ToDateTime(reader["Timestamp"]), // fallback just in case
                        IsEdited = Convert.ToBoolean(reader["IsEdited"]),
                        LikesCount = Convert.ToInt32(reader["LikesCount"]),
                        CommentsCount = Convert.ToInt32(reader["CommentsCount"]),
                        IsLikedByMe = Convert.ToBoolean(reader["IsLikedByMe"])
                    });
                }
                return Ok(posts);
            }
            catch (Exception ex) { return StatusCode(500, $"Error fetching posts: {ex.Message}"); }
        }

        [HttpPost]
        public async Task<IActionResult> CreatePost([FromBody] CreatePostRequest request)
        {
            try
            {
                string conn = _configuration.GetConnectionString("DefaultConnection")!;
                using var connection = new MySqlConnection(conn);
                await connection.OpenAsync();
                var query = "INSERT INTO communityposts (UserId, AuthorName, PetName, Content, ImageUrl, Timestamp, IsEdited) VALUES (@UserId, '', '', @Content, @ImageUrls, NOW(), 0)";
                using var cmd = new MySqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@UserId", request.UserId);
                cmd.Parameters.AddWithValue("@Content", request.Content);
                cmd.Parameters.AddWithValue("@ImageUrls", (object?)request.ImageUrls ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, $"Error creating post: {ex.Message}"); }
        }

        [HttpPost("{postId}/like")]
        public async Task<IActionResult> ToggleLike(int postId, [FromBody] LikeRequest request)
        {
            try
            {
                string conn = _configuration.GetConnectionString("DefaultConnection")!;
                using var connection = new MySqlConnection(conn);
                await connection.OpenAsync();
                var check = "SELECT Id FROM communitylikes WHERE PostId = @PostId AND UserId = @UserId";
                using var checkCmd = new MySqlCommand(check, connection);
                checkCmd.Parameters.AddWithValue("@PostId", postId);
                checkCmd.Parameters.AddWithValue("@UserId", request.UserId);
                var exists = await checkCmd.ExecuteScalarAsync();
                bool isLiked;
                if (exists != null)
                {
                    using var del = new MySqlCommand("DELETE FROM communitylikes WHERE PostId = @PostId AND UserId = @UserId", connection);
                    del.Parameters.AddWithValue("@PostId", postId); del.Parameters.AddWithValue("@UserId", request.UserId);
                    await del.ExecuteNonQueryAsync(); isLiked = false;
                }
                else
                {
                    using var ins = new MySqlCommand("INSERT INTO communitylikes (PostId, UserId) VALUES (@PostId, @UserId)", connection);
                    ins.Parameters.AddWithValue("@PostId", postId); ins.Parameters.AddWithValue("@UserId", request.UserId);
                    await ins.ExecuteNonQueryAsync(); isLiked = true;
                }
                return Ok(new { success = true, isLiked });
            }
            catch (Exception ex) { return StatusCode(500, $"Error toggling like: {ex.Message}"); }
        }

        [HttpGet("{postId}/comments")]
        public async Task<IActionResult> GetComments(int postId, [FromQuery] int userId = 0)
        {
            try
            {
                string conn = _configuration.GetConnectionString("DefaultConnection")!;
                var comments = new List<object>();
                using var connection = new MySqlConnection(conn);
                await connection.OpenAsync();
                var query = @"SELECT c.Id, c.PostId, c.UserId, c.ParentCommentId, c.Content, c.CreatedAt,
                        u.FullName AS AuthorName,
                        parent_u.FullName AS ParentAuthorName,
                        (SELECT COUNT(*) FROM communitycommentlikes cl WHERE cl.CommentId = c.Id) AS LikeCount,
                        EXISTS(SELECT 1 FROM communitycommentlikes cl WHERE cl.CommentId = c.Id AND cl.UserId = @UserId) AS IsLikedByMe
                    FROM communitycomments c
                    LEFT JOIN users u ON c.UserId = u.Id
                    LEFT JOIN communitycomments parent_c ON c.ParentCommentId = parent_c.Id
                    LEFT JOIN users parent_u ON parent_c.UserId = parent_u.Id
                    WHERE c.PostId = @PostId ORDER BY c.CreatedAt ASC;";
                using var cmd = new MySqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@PostId", postId);
                cmd.Parameters.AddWithValue("@UserId", userId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    comments.Add(new {
                        Id = Convert.ToInt32(reader["Id"]), 
                        PostId = Convert.ToInt32(reader["PostId"]),
                        UserId = Convert.ToInt32(reader["UserId"]),
                        ParentCommentId = reader["ParentCommentId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["ParentCommentId"]),
                        AuthorName = reader["AuthorName"] == DBNull.Value ? "Unknown" : reader["AuthorName"].ToString(),
                        ParentAuthorName = reader["ParentAuthorName"] == DBNull.Value ? null : reader["ParentAuthorName"].ToString(),
                        Content = reader["Content"].ToString(),
                        CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
                        LikeCount = Convert.ToInt32(reader["LikeCount"]),
                        IsLikedByMe = Convert.ToBoolean(reader["IsLikedByMe"])
                    });
                }
                return Ok(comments);
            }
            catch (Exception ex) { return StatusCode(500, $"Error fetching comments: {ex.Message}"); }
        }

        [HttpPost("{postId}/comments")]
        public async Task<IActionResult> AddComment(int postId, [FromBody] AddCommentRequest request)
        {
            try
            {
                string conn = _configuration.GetConnectionString("DefaultConnection")!;
                using var connection = new MySqlConnection(conn);
                await connection.OpenAsync();
                var query = "INSERT INTO communitycomments (PostId, UserId, ParentCommentId, Content, CreatedAt) VALUES (@PostId, @UserId, @ParentCommentId, @Content, NOW())";
                using var cmd = new MySqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@PostId", postId);
                cmd.Parameters.AddWithValue("@UserId", request.UserId);
                cmd.Parameters.AddWithValue("@ParentCommentId", (object?)request.ParentCommentId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Content", request.Content);
                await cmd.ExecuteNonQueryAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, $"Error adding comment: {ex.Message}"); }
        }

        [HttpPost("comments/{commentId}/like")]
        public async Task<IActionResult> ToggleCommentLike(int commentId, [FromBody] LikeRequest request)
        {
            try
            {
                string conn = _configuration.GetConnectionString("DefaultConnection")!;
                using var connection = new MySqlConnection(conn);
                await connection.OpenAsync();
                var check = "SELECT Id FROM communitycommentlikes WHERE CommentId = @CommentId AND UserId = @UserId";
                using var checkCmd = new MySqlCommand(check, connection);
                checkCmd.Parameters.AddWithValue("@CommentId", commentId);
                checkCmd.Parameters.AddWithValue("@UserId", request.UserId);
                var exists = await checkCmd.ExecuteScalarAsync();
                bool isLiked;
                if (exists != null)
                {
                    using var del = new MySqlCommand("DELETE FROM communitycommentlikes WHERE CommentId = @CommentId AND UserId = @UserId", connection);
                    del.Parameters.AddWithValue("@CommentId", commentId); del.Parameters.AddWithValue("@UserId", request.UserId);
                    await del.ExecuteNonQueryAsync(); isLiked = false;
                }
                else
                {
                    using var ins = new MySqlCommand("INSERT INTO communitycommentlikes (CommentId, UserId) VALUES (@CommentId, @UserId)", connection);
                    ins.Parameters.AddWithValue("@CommentId", commentId); ins.Parameters.AddWithValue("@UserId", request.UserId);
                    await ins.ExecuteNonQueryAsync(); isLiked = true;
                }
                return Ok(new { success = true, isLiked });
            }
            catch (Exception ex) { return StatusCode(500, $"Error toggling comment like: {ex.Message}"); }
        }

        [HttpDelete("{postId}")]
        public async Task<IActionResult> DeletePost(int postId)
        {
            try
            {
                string conn = _configuration.GetConnectionString("DefaultConnection")!; using var connection = new MySqlConnection(conn);
                await connection.OpenAsync();
                
                using var transaction = await connection.BeginTransactionAsync();
                
                using var cmdLikes = new MySqlCommand("DELETE FROM communitylikes WHERE PostId = @Id", connection, transaction);
                cmdLikes.Parameters.AddWithValue("@Id", postId);
                await cmdLikes.ExecuteNonQueryAsync();
                
                using var cmdCommentLikes = new MySqlCommand("DELETE FROM communitycommentlikes WHERE CommentId IN (SELECT Id FROM communitycomments WHERE PostId = @Id)", connection, transaction);
                cmdCommentLikes.Parameters.AddWithValue("@Id", postId);
                await cmdCommentLikes.ExecuteNonQueryAsync();
                
                using var cmdComments = new MySqlCommand("DELETE FROM communitycomments WHERE PostId = @Id", connection, transaction);
                cmdComments.Parameters.AddWithValue("@Id", postId);
                await cmdComments.ExecuteNonQueryAsync();

                using var cmdPost = new MySqlCommand("DELETE FROM communityposts WHERE Id = @Id", connection, transaction);
                cmdPost.Parameters.AddWithValue("@Id", postId);
                var rows = await cmdPost.ExecuteNonQueryAsync();
                
                await transaction.CommitAsync();

                if (rows == 0) return NotFound();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, $"Error deleting post: {ex.Message}"); }
        }

        [HttpPut("{postId}")]
        public async Task<IActionResult> EditPost(int postId, [FromBody] EditPostRequest request)
        {
            try
            {
                string conn = _configuration.GetConnectionString("DefaultConnection")!; using var connection = new MySqlConnection(conn);
                await connection.OpenAsync();
                
                using var cmd = new MySqlCommand("UPDATE communityposts SET Content = @Content, IsEdited = 1 WHERE Id = @Id", connection);
                cmd.Parameters.AddWithValue("@Id", postId);
                cmd.Parameters.AddWithValue("@Content", request.Content);
                var rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0) return NotFound();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, $"Error editing post: {ex.Message}"); }
        }

        [HttpDelete("comments/{commentId}")]
        public async Task<IActionResult> DeleteComment(int commentId)
        {
            try
            {
                string conn = _configuration.GetConnectionString("DefaultConnection")!; using var connection = new MySqlConnection(conn);
                await connection.OpenAsync();
                
                // Usually deleting a parent comment should cascade replies, this depends on DB schema
                using var cmd = new MySqlCommand("DELETE FROM communitycomments WHERE Id = @Id OR ParentCommentId = @Id", connection);
                cmd.Parameters.AddWithValue("@Id", commentId);
                var rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0) return NotFound();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, $"Error deleting comment: {ex.Message}"); }
        }
    }

    public class EditPostRequest { public string Content { get; set; } = string.Empty; }
    public class CreatePostRequest { public int UserId { get; set; } public int? PetId { get; set; } public string AuthorName { get; set; } = string.Empty; public string PetName { get; set; } = string.Empty; public string Content { get; set; } = string.Empty; public string? ImageUrls { get; set; } }
    public class AddCommentRequest { public int UserId { get; set; } public int? ParentCommentId { get; set; } public string Content { get; set; } = string.Empty; }
    public class LikeRequest { public int UserId { get; set; } }
}





