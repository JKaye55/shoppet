using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using ShoppetAPI.Services;

namespace ShoppetAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CommunityController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public CommunityController(IConfiguration configuration, IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

    private string ConnectionString =>
        _configuration.GetConnectionString("SharedSqlServer")
        ?? throw new InvalidOperationException("Connection string 'SharedSqlServer' was not found.");

    [HttpGet]
    public async Task<IActionResult> GetPosts([FromQuery] int userId = 0)
    {
        try
        {
            var posts = new List<object>();
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            const string query = """
                SELECT
                    p.Id,
                    p.UserId,
                    p.PetId,
                    ISNULL(u.FullName, 'Unknown') AS AuthorName,
                    ISNULL(u.ProfilePicture, '') AS ProfilePicture,
                    ISNULL(u.IsPremium, 0) AS IsAuthorPremium,
                    ISNULL(u.Role, '') AS AuthorRole,
                    ISNULL(pp.PetName, '') AS PetName,
                    ISNULL(p.Caption, '') AS Content,
                    ISNULL(p.ImageUrl, '') AS ImageUrls,
                    p.PostedAt,
                    ISNULL(p.IsEdited, 0) AS IsEdited,
                    (SELECT COUNT(*) FROM CommunityLikes cl WHERE cl.PostId = p.Id) AS LikesCount,
                    (SELECT COUNT(*) FROM CommunityComments cc WHERE cc.PostId = p.Id) AS CommentsCount,
                    CASE WHEN EXISTS(
                        SELECT 1 FROM CommunityLikes cl
                        WHERE cl.PostId = p.Id AND cl.UserId = @UserId
                    ) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS IsLikedByMe
                FROM CommunityPosts p
                LEFT JOIN UserAccounts u ON u.Id = p.UserId
                LEFT JOIN PetProfiles pp ON pp.Id = p.PetId
                ORDER BY p.PostedAt DESC;
                """;

            await using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@UserId", userId);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var postedAt = reader.IsDBNull(reader.GetOrdinal("PostedAt")) ? DateTime.UtcNow : reader.GetDateTime(reader.GetOrdinal("PostedAt"));
                var rawPic = reader.IsDBNull(reader.GetOrdinal("ProfilePicture")) ? "" : reader.GetString(reader.GetOrdinal("ProfilePicture"));
                posts.Add(new
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    UserId = reader.IsDBNull(reader.GetOrdinal("UserId"))
                        ? 0
                        : reader.GetInt32(reader.GetOrdinal("UserId")),
                    PetId = reader.IsDBNull(reader.GetOrdinal("PetId"))
                        ? (int?)null
                        : reader.GetInt32(reader.GetOrdinal("PetId")),
                    AuthorName = reader.IsDBNull(reader.GetOrdinal("AuthorName")) ? "Unknown" : reader.GetString(reader.GetOrdinal("AuthorName")),
                    ProfilePicture = MediaUrls.Web(rawPic, _configuration),
                    IsAuthorPremium = !reader.IsDBNull(reader.GetOrdinal("IsAuthorPremium")) && reader.GetBoolean(reader.GetOrdinal("IsAuthorPremium")),
                    AuthorRole = reader.IsDBNull(reader.GetOrdinal("AuthorRole")) ? "" : reader.GetString(reader.GetOrdinal("AuthorRole")),
                    PetName = reader.IsDBNull(reader.GetOrdinal("PetName")) ? string.Empty : reader.GetString(reader.GetOrdinal("PetName")),
                    Content = reader.IsDBNull(reader.GetOrdinal("Content")) ? string.Empty : reader.GetString(reader.GetOrdinal("Content")),
                    ImageUrls = MediaUrls.Web(reader.IsDBNull(reader.GetOrdinal("ImageUrls")) ? "" : reader.GetString(reader.GetOrdinal("ImageUrls")),_configuration),
                    Timestamp = postedAt,
                    CreatedAt = postedAt,
                    IsEdited = !reader.IsDBNull(reader.GetOrdinal("IsEdited")) && reader.GetBoolean(reader.GetOrdinal("IsEdited")),
                    LikesCount = reader.IsDBNull(reader.GetOrdinal("LikesCount")) ? 0 : reader.GetInt32(reader.GetOrdinal("LikesCount")),
                    CommentsCount = reader.IsDBNull(reader.GetOrdinal("CommentsCount")) ? 0 : reader.GetInt32(reader.GetOrdinal("CommentsCount")),
                    IsLikedByMe = !reader.IsDBNull(reader.GetOrdinal("IsLikedByMe")) && reader.GetBoolean(reader.GetOrdinal("IsLikedByMe"))
                });
            }

            return Ok(posts);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error fetching posts: {ex.Message}");
        }
    }

    [HttpPost("media")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> UploadMedia([FromForm] List<IFormFile> files)
    {
        if (files is null || files.Count == 0)
            return BadRequest("No image files were provided.");

        if (files.Count > 5)
            return BadRequest("A maximum of 5 images is allowed.");

        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp"
        };

        var uploadFolder = Path.Combine(
            _environment.ContentRootPath,
            "wwwroot",
            "uploads",
            "community");

        Directory.CreateDirectory(uploadFolder);
        var urls = new List<string>();

        foreach (var file in files)
        {
            if (file.Length <= 0) continue;

            if (file.Length > 8_000_000)
                return BadRequest("Each image must be 8 MB or smaller.");

            var extension = Path.GetExtension(file.FileName);
            if (!allowedExtensions.Contains(extension))
                return BadRequest("Only JPG, JPEG, PNG, and WEBP images are supported.");

            var safeName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            var diskPath = Path.Combine(uploadFolder, safeName);

            await using var stream = System.IO.File.Create(diskPath);
            await file.CopyToAsync(stream);

            var publicBaseUrl = (_configuration["PublicApiBaseUrl"] ?? _configuration["PublicBaseUrl"] ?? "http://localhost:5020").TrimEnd('/');
            urls.Add($"{publicBaseUrl}/uploads/community/{safeName}");
        }

        return Ok(urls);
    }

    [HttpPost]
    public async Task<IActionResult> CreatePost([FromBody] CreatePostRequest request)
    {
        if (request.UserId <= 0)
            return BadRequest("A valid user is required.");

        if (string.IsNullOrWhiteSpace(request.Content) && string.IsNullOrWhiteSpace(request.ImageUrls))
            return BadRequest("A post must contain text or an image.");

        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            const string query = """
                INSERT INTO CommunityPosts
                    (UserId, PetId, Caption, ImageUrl, LikeCount, PostedAt, IsEdited)
                VALUES
                    (@UserId, @PetId, @Caption, @ImageUrl, 0, SYSDATETIME(), 0);

                SELECT CAST(SCOPE_IDENTITY() AS int);
                """;

            await using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@UserId", request.UserId);
            cmd.Parameters.AddWithValue(
                "@PetId",
                request.PetId.HasValue ? request.PetId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@Caption", request.Content.Trim());
            cmd.Parameters.AddWithValue(
                "@ImageUrl",
                string.IsNullOrWhiteSpace(request.ImageUrls)
                    ? DBNull.Value
                    : request.ImageUrls);

            var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(new { success = true, id = newId });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error creating post: {ex.Message}");
        }
    }

    [HttpPost("{postId:int}/like")]
    public async Task<IActionResult> ToggleLike(int postId, [FromBody] LikeRequest request)
    {
        if (request.UserId <= 0)
            return BadRequest("A valid user is required.");

        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            if (!await RbacService.IsPetOwnerAsync(connection, request.UserId))
                return StatusCode(403,"Access denied.");

            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

            var isLiked = false;

            await using (var check = new SqlCommand(
                "SELECT Id FROM CommunityLikes WHERE PostId=@PostId AND UserId=@UserId",
                connection,
                transaction))
            {
                check.Parameters.AddWithValue("@PostId", postId);
                check.Parameters.AddWithValue("@UserId", request.UserId);
                var existing = await check.ExecuteScalarAsync();

                if (existing is null)
                {
                    await using var insert = new SqlCommand(
                        "INSERT INTO CommunityLikes (PostId, UserId, CreatedAt) VALUES (@PostId,@UserId,SYSDATETIME())",
                        connection,
                        transaction);
                    insert.Parameters.AddWithValue("@PostId", postId);
                    insert.Parameters.AddWithValue("@UserId", request.UserId);
                    await insert.ExecuteNonQueryAsync();
                    isLiked = true;
                }
                else
                {
                    await using var delete = new SqlCommand(
                        "DELETE FROM CommunityLikes WHERE PostId=@PostId AND UserId=@UserId",
                        connection,
                        transaction);
                    delete.Parameters.AddWithValue("@PostId", postId);
                    delete.Parameters.AddWithValue("@UserId", request.UserId);
                    await delete.ExecuteNonQueryAsync();
                }
            }

            await using (var syncCount = new SqlCommand(
                """
                UPDATE CommunityPosts
                SET LikeCount = (SELECT COUNT(*) FROM CommunityLikes WHERE PostId=@PostId)
                WHERE Id=@PostId
                """,
                connection,
                transaction))
            {
                syncCount.Parameters.AddWithValue("@PostId", postId);
                await syncCount.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            return Ok(new { success = true, isLiked });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error toggling like: {ex.Message}");
        }
    }

    [HttpGet("{postId:int}/comments")]
    public async Task<IActionResult> GetComments(int postId, [FromQuery] int userId = 0)
    {
        try
        {
            var comments = new List<object>();
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            const string query = """
                SELECT
                    c.Id,
                    c.PostId,
                    c.UserId,
                    c.ParentCommentId,
                    COALESCE(NULLIF(c.Content, ''), c.Body, '') AS Content,
                    c.CreatedAt,
                    COALESCE(u.FullName, NULLIF(c.AuthorName, ''), 'Unknown') AS AuthorName,
                    ISNULL(u.ProfilePicture, '') AS ProfilePicture,
                    ISNULL(u.IsPremium, 0) AS IsAuthorPremium,
                    ISNULL(u.Role, '') AS AuthorRole,
                    parent_u.FullName AS ParentAuthorName,
                    (SELECT COUNT(*) FROM CommunityCommentLikes l WHERE l.CommentId=c.Id) AS LikeCount,
                    CASE WHEN EXISTS(
                        SELECT 1 FROM CommunityCommentLikes l
                        WHERE l.CommentId=c.Id AND l.UserId=@UserId
                    ) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS IsLikedByMe
                FROM CommunityComments c
                LEFT JOIN UserAccounts u ON u.Id=c.UserId
                LEFT JOIN CommunityComments parent_c ON parent_c.Id=c.ParentCommentId
                LEFT JOIN UserAccounts parent_u ON parent_u.Id=parent_c.UserId
                WHERE c.PostId=@PostId
                ORDER BY c.CreatedAt ASC;
                """;

            await using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@PostId", postId);
            cmd.Parameters.AddWithValue("@UserId", userId);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var rawPic = reader.IsDBNull(reader.GetOrdinal("ProfilePicture")) ? "" : reader.GetString(reader.GetOrdinal("ProfilePicture"));
                comments.Add(new
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    PostId = reader.GetInt32(reader.GetOrdinal("PostId")),
                    UserId = reader.IsDBNull(reader.GetOrdinal("UserId"))
                        ? 0
                        : reader.GetInt32(reader.GetOrdinal("UserId")),
                    ParentCommentId = reader.IsDBNull(reader.GetOrdinal("ParentCommentId"))
                        ? (int?)null
                        : reader.GetInt32(reader.GetOrdinal("ParentCommentId")),
                    AuthorName = reader.IsDBNull(reader.GetOrdinal("AuthorName"))
                        ? "Unknown"
                        : reader.GetString(reader.GetOrdinal("AuthorName")),
                    ProfilePicture = MediaUrls.Web(rawPic, _configuration),
                    IsAuthorPremium = !reader.IsDBNull(reader.GetOrdinal("IsAuthorPremium")) && reader.GetBoolean(reader.GetOrdinal("IsAuthorPremium")),
                    AuthorRole = reader.IsDBNull(reader.GetOrdinal("AuthorRole")) ? "" : reader.GetString(reader.GetOrdinal("AuthorRole")),
                    ParentAuthorName = reader.IsDBNull(reader.GetOrdinal("ParentAuthorName"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("ParentAuthorName")),
                    Content = reader.IsDBNull(reader.GetOrdinal("Content"))
                        ? string.Empty
                        : reader.GetString(reader.GetOrdinal("Content")),
                    CreatedAt = reader.IsDBNull(reader.GetOrdinal("CreatedAt"))
                        ? DateTime.UtcNow
                        : reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                    LikeCount = reader.IsDBNull(reader.GetOrdinal("LikeCount"))
                        ? 0
                        : reader.GetInt32(reader.GetOrdinal("LikeCount")),
                    IsLikedByMe = !reader.IsDBNull(reader.GetOrdinal("IsLikedByMe")) && reader.GetBoolean(reader.GetOrdinal("IsLikedByMe"))
                });
            }

            return Ok(comments);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error fetching comments: {ex.Message}");
        }
    }

    [HttpPost("{postId:int}/comments")]
    public async Task<IActionResult> AddComment(int postId, [FromBody] AddCommentRequest request)
    {
        if (request.UserId <= 0 || string.IsNullOrWhiteSpace(request.Content)||request.Content.Length>300)
            return BadRequest("A valid user and comment are required.");

        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            if (!await RbacService.IsPetOwnerAsync(connection, request.UserId))
                return StatusCode(403,"Access denied.");

            const string query = """
                DECLARE @AuthorName NVARCHAR(150) =
                    LEFT(ISNULL((SELECT FullName FROM UserAccounts WHERE Id=@UserId), 'Unknown'),120);

                IF NOT EXISTS(SELECT 1 FROM CommunityPosts WHERE Id=@PostId) THROW 51000,'Post not found.',1;
                IF @ParentCommentId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM CommunityComments WHERE Id=@ParentCommentId AND PostId=@PostId) THROW 51000,'Reply parent is not in this post.',1;
                INSERT INTO CommunityComments
                    (PostId, UserId, AuthorName, Body, IsGuest, CreatedAt, ParentCommentId, Content)
                VALUES
                    (@PostId, @UserId, @AuthorName, LEFT(@Content,300), 0, SYSDATETIME(), @ParentCommentId, @Content);

                SELECT CAST(SCOPE_IDENTITY() AS int);
                """;

            await using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@PostId", postId);
            cmd.Parameters.AddWithValue("@UserId", request.UserId);
            cmd.Parameters.AddWithValue(
                "@ParentCommentId",
                request.ParentCommentId.HasValue ? request.ParentCommentId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@Content", request.Content.Trim());

            var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(new { success = true, id });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error adding comment: {ex.Message}");
        }
    }

    [HttpPost("comments/{commentId:int}/like")]
    public async Task<IActionResult> ToggleCommentLike(int commentId, [FromBody] LikeRequest request)
    {
        if (request.UserId <= 0)
            return BadRequest("A valid user is required.");

        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            if (!await RbacService.IsPetOwnerAsync(connection, request.UserId))
                return StatusCode(403,"Access denied.");

            const string checkSql =
                "SELECT Id FROM CommunityCommentLikes WHERE CommentId=@CommentId AND UserId=@UserId";

            await using var check = new SqlCommand(checkSql, connection);
            check.Parameters.AddWithValue("@CommentId", commentId);
            check.Parameters.AddWithValue("@UserId", request.UserId);

            var existing = await check.ExecuteScalarAsync();
            var isLiked = existing is null;

            var sql = isLiked
                ? "INSERT INTO CommunityCommentLikes (CommentId,UserId,CreatedAt) VALUES (@CommentId,@UserId,SYSDATETIME())"
                : "DELETE FROM CommunityCommentLikes WHERE CommentId=@CommentId AND UserId=@UserId";

            await using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@CommentId", commentId);
            cmd.Parameters.AddWithValue("@UserId", request.UserId);
            await cmd.ExecuteNonQueryAsync();

            return Ok(new { success = true, isLiked });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error toggling comment like: {ex.Message}");
        }
    }

    [HttpDelete("{postId:int}")]
    public async Task<IActionResult> DeletePost(int postId, [FromQuery] int userId)
    {
        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

            var role = await RbacService.GetActiveRoleAsync(connection, userId, transaction);
            if (role is null) return Unauthorized();

            await using (var ownership = new SqlCommand(
                "SELECT UserId FROM CommunityPosts WHERE Id=@Id",
                connection,
                transaction))
            {
                ownership.Parameters.AddWithValue("@Id", postId);
                var owner = await ownership.ExecuteScalarAsync();

                if (owner is null) return NotFound();
                if (!RbacService.IsAdmin(role) && Convert.ToInt32(owner) != userId)
                    return StatusCode(403,"Access denied.");
            }

            var commands = new[]
            {
                "DELETE FROM CommunityCommentLikes WHERE CommentId IN (SELECT Id FROM CommunityComments WHERE PostId=@Id)",
                "DELETE FROM CommunityComments WHERE PostId=@Id",
                "DELETE FROM CommunityLikes WHERE PostId=@Id",
                "DELETE FROM CommunityPosts WHERE Id=@Id"
            };

            var affected = 0;
            foreach (var sql in commands)
            {
                await using var cmd = new SqlCommand(sql, connection, transaction);
                cmd.Parameters.AddWithValue("@Id", postId);
                affected = await cmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            return affected == 0 ? NotFound() : Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error deleting post: {ex.Message}");
        }
    }

    [HttpPut("{postId:int}")]
    public async Task<IActionResult> EditPost(int postId, [FromBody] EditPostRequest request)
    {
        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            var role = await RbacService.GetActiveRoleAsync(connection, request.UserId);
            if (role is null) return Unauthorized();

            const string sql = """
                UPDATE CommunityPosts
                SET Caption=@Content,
                    ImageUrl=@ImageUrls,
                    PetId=@PetId,
                    IsEdited=1
                WHERE Id=@Id
                  AND (@IsAdmin=1 OR UserId=@UserId)
                """;

            await using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Id", postId);
            cmd.Parameters.AddWithValue("@UserId", request.UserId);
            cmd.Parameters.AddWithValue("@IsAdmin", RbacService.IsAdmin(role) ? 1 : 0);
            cmd.Parameters.AddWithValue("@Content", request.Content?.Trim() ?? string.Empty);
            cmd.Parameters.AddWithValue(
                "@ImageUrls",
                string.IsNullOrWhiteSpace(request.ImageUrls) ? DBNull.Value : request.ImageUrls);
            cmd.Parameters.AddWithValue(
                "@PetId",
                request.PetId.HasValue ? request.PetId.Value : DBNull.Value);

            var rows = await cmd.ExecuteNonQueryAsync();
            return rows == 0 ? NotFound() : Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error editing post: {ex.Message}");
        }
    }

    [HttpDelete("comments/{commentId:int}")]
    public async Task<IActionResult> DeleteComment(int commentId, [FromQuery] int userId)
    {
        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

            var role = await RbacService.GetActiveRoleAsync(connection, userId, transaction);
            if (role is null) return Unauthorized();

            await using (var ownership = new SqlCommand(
                "SELECT UserId FROM CommunityComments WHERE Id=@Id",
                connection,
                transaction))
            {
                ownership.Parameters.AddWithValue("@Id", commentId);
                var owner = await ownership.ExecuteScalarAsync();
                if (owner is null) return NotFound();
                if (!RbacService.IsAdmin(role) && Convert.ToInt32(owner) != userId)
                    return StatusCode(403,"Access denied.");
            }

            await using (var likes = new SqlCommand(
                """
                DELETE FROM CommunityCommentLikes
                WHERE CommentId=@Id
                   OR CommentId IN (SELECT Id FROM CommunityComments WHERE ParentCommentId=@Id)
                """,
                connection,
                transaction))
            {
                likes.Parameters.AddWithValue("@Id", commentId);
                await likes.ExecuteNonQueryAsync();
            }

            await using var comments = new SqlCommand(
                "DELETE FROM CommunityComments WHERE Id=@Id OR ParentCommentId=@Id",
                connection,
                transaction);
            comments.Parameters.AddWithValue("@Id", commentId);
            var rows = await comments.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
            return rows == 0 ? NotFound() : Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error deleting comment: {ex.Message}");
        }
    }

    [HttpGet("rankings")]
    public async Task<IActionResult> GetRankings([FromQuery] int limit = 20)
    {
        try
        {
            var rankings = new List<object>();
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            const string query = """
                SELECT TOP (@Limit)
                    u.Id AS UserId,
                    ISNULL(u.FullName, 'Pet Lover') AS FullName,
                    ISNULL(u.ProfilePicture, '') AS Avatar,
                    (SELECT COUNT(*) FROM CommunityPosts p WHERE p.UserId = u.Id) AS PostCount,
                    ISNULL((
                        SELECT COUNT(*)
                        FROM CommunityLikes cl
                        INNER JOIN CommunityPosts cp ON cp.Id = cl.PostId
                        WHERE cp.UserId = u.Id
                    ), 0) AS LikesReceived,
                    (SELECT COUNT(*) FROM CommunityComments cc WHERE cc.UserId = u.Id) AS CommentCount,
                    ISNULL(u.IsPremium, 0) AS IsPremium,
                    ISNULL(u.Role, '') AS Role
                FROM UserAccounts u
                WHERE ISNULL(u.IsDisabled, 0) = 0
                ORDER BY (
                    (SELECT COUNT(*) FROM CommunityPosts p WHERE p.UserId = u.Id) * 3 +
                    ISNULL((
                        SELECT COUNT(*)
                        FROM CommunityLikes cl
                        INNER JOIN CommunityPosts cp ON cp.Id = cl.PostId
                        WHERE cp.UserId = u.Id
                    ), 0) * 2 +
                    (SELECT COUNT(*) FROM CommunityComments cc WHERE cc.UserId = u.Id)
                ) DESC, u.Id ASC;
                """;

            await using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@Limit", limit <= 0 ? 20 : limit);

            await using var reader = await cmd.ExecuteReaderAsync();
            int rank = 1;
            while (await reader.ReadAsync())
            {
                var postCount = reader.GetInt32(reader.GetOrdinal("PostCount"));
                var likesReceived = reader.GetInt32(reader.GetOrdinal("LikesReceived"));
                var commentCount = reader.GetInt32(reader.GetOrdinal("CommentCount"));
                var score = (postCount * 3) + (likesReceived * 2) + commentCount;
                var rawAvatar = reader.GetString(reader.GetOrdinal("Avatar"));
                var isPrem = !reader.IsDBNull(reader.GetOrdinal("IsPremium")) && reader.GetBoolean(reader.GetOrdinal("IsPremium"));
                var role = reader.IsDBNull(reader.GetOrdinal("Role")) ? "" : reader.GetString(reader.GetOrdinal("Role"));

                rankings.Add(new
                {
                    Rank = rank++,
                    UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                    FullName = reader.GetString(reader.GetOrdinal("FullName")),
                    Avatar = MediaUrls.Web(rawAvatar, _configuration),
                    PostCount = postCount,
                    LikesReceived = likesReceived,
                    CommentCount = commentCount,
                    Score = score,
                    IsPremium = isPrem,
                    Role = role
                });
            }

            return Ok(rankings);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error fetching rankings: {ex.Message}");
        }
    }
}

public class EditPostRequest
{
    public int UserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ImageUrls { get; set; }
    public int? PetId { get; set; }
    public string PetName { get; set; } = string.Empty;
}

public class CreatePostRequest
{
    public int UserId { get; set; }
    public int? PetId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string PetName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? ImageUrls { get; set; }
}

public class AddCommentRequest
{
    public int UserId { get; set; }
    public int? ParentCommentId { get; set; }
    public string Content { get; set; } = string.Empty;
}

public class LikeRequest
{
    public int UserId { get; set; }
}
