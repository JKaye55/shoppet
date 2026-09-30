using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ShoppetAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MarketplaceController : ControllerBase
{
    private readonly IConfiguration _configuration;
    public MarketplaceController(IConfiguration configuration) => _configuration = configuration;

    private string ConnectionString =>
        _configuration.GetConnectionString("SharedSqlServer")
        ?? throw new InvalidOperationException("SharedSqlServer connection string not found.");

    [HttpGet]
    public async Task<IActionResult> GetListings([FromQuery] string? category = null, [FromQuery] string? search = null)
        => Ok(await LoadListingsAsync(null, category, search));

    [HttpGet("my/{userId:int}")]
    public async Task<IActionResult> GetMyListings(int userId)
        => Ok(await LoadListingsAsync(userId, null, null));

    [HttpPost]
    public async Task<IActionResult> CreateListing([FromBody] CreateListingRequest request)
    {
        var validationError = ValidateListing(request);
        if (validationError is not null) return BadRequest(validationError);

        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var cmd = new SqlCommand(@"
                INSERT INTO MarketplaceListings
                    (SellerUserId, Title, Category, ItemCondition, Price, Description, Location,
                     ImageUrl, ImageUrls, Status, CreatedAt, UpdatedAt)
                OUTPUT INSERTED.Id
                VALUES
                    (@UserId,@Title,@Category,@Condition,@Price,@Description,@Location,
                     @ImageUrl,@ImageUrls,'Available',SYSDATETIME(),SYSDATETIME());", connection);
            AddListingParameters(cmd, request.UserId, request.Title, request.Description, request.Price, request.Category, request.Condition, request.Location, request.ImageUrls);
            int id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(new { success = true, id });
        }
        catch (Exception ex) { return StatusCode(500, $"Error: {ex.Message}"); }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> EditListing(int id, [FromBody] EditListingRequest request)
    {
        var validationError = ValidateListing(request);
        if (validationError is not null) return BadRequest(validationError);

        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var cmd = new SqlCommand(@"
                UPDATE MarketplaceListings
                SET Title=@Title, Description=@Description, Price=@Price, Category=@Category,
                    ItemCondition=@Condition, Location=@Location,
                    ImageUrl=@ImageUrl, ImageUrls=@ImageUrls, UpdatedAt=SYSDATETIME()
                WHERE Id=@Id AND SellerUserId=@UserId;", connection);
            cmd.Parameters.AddWithValue("@Id", id);
            AddListingParameters(cmd, request.UserId, request.Title, request.Description, request.Price, request.Category, request.Condition, request.Location, request.ImageUrls);
            return await cmd.ExecuteNonQueryAsync() > 0 ? Ok(new { success = true }) : NotFound("Listing not found.");
        }
        catch (Exception ex) { return StatusCode(500, $"Error: {ex.Message}"); }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteListing(int id, [FromQuery] int userId)
    {
        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var cmd = new SqlCommand("DELETE FROM MarketplaceListings WHERE Id=@Id AND SellerUserId=@UserId", connection);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@UserId", userId);
            return await cmd.ExecuteNonQueryAsync() > 0 ? Ok(new { success = true }) : NotFound("Listing not found.");
        }
        catch (Exception ex) { return StatusCode(500, $"Error: {ex.Message}"); }
    }

    private async Task<List<object>> LoadListingsAsync(int? userId, string? category, string? search)
    {
        var result = new List<object>();
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        var conditions = new List<string>();
        if (!userId.HasValue) conditions.Add("m.Status='Available'");
        if (userId.HasValue) conditions.Add("m.SellerUserId=@UserId");
        if (!string.IsNullOrWhiteSpace(category)) conditions.Add("m.Category=@Category");
        if (!string.IsNullOrWhiteSpace(search)) conditions.Add("(m.Title LIKE @Search OR m.Description LIKE @Search OR m.Category LIKE @Search)");

        string where = conditions.Count == 0 ? "" : "WHERE " + string.Join(" AND ", conditions);
        string sql = $@"
            SELECT m.Id,m.SellerUserId,m.Title,m.Description,m.Price,m.Category,m.ItemCondition,m.Location,
                   COALESCE(m.ImageUrls,m.ImageUrl,'') AS ImageUrls,m.Status,m.CreatedAt,
                   u.FullName,u.ProfilePicture,u.FacebookUrl,u.InstagramUrl,u.OtherSocialUrl,
                   u.ShowSocialLinksOnMarketplace
            FROM MarketplaceListings m
            LEFT JOIN UserAccounts u ON u.Id=m.SellerUserId
            {where}
            ORDER BY m.CreatedAt DESC;";

        await using var cmd = new SqlCommand(sql, connection);
        if (userId.HasValue) cmd.Parameters.AddWithValue("@UserId", userId.Value);
        if (!string.IsNullOrWhiteSpace(category)) cmd.Parameters.AddWithValue("@Category", category);
        if (!string.IsNullOrWhiteSpace(search)) cmd.Parameters.AddWithValue("@Search", $"%{search}%");

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            bool showSocial = !reader.IsDBNull(16) && reader.GetBoolean(16);
            result.Add(new
            {
                Id = reader.GetInt32(0),
                UserId = reader.GetInt32(1),
                SellerName = reader.IsDBNull(11) ? "ShoppetCare User" : reader.GetString(11),
                SellerProfilePic = reader.IsDBNull(12) ? "" : reader.GetString(12),
                Title = reader.GetString(2),
                Description = reader.IsDBNull(3) ? "" : reader.GetString(3),
                Price = reader.GetDecimal(4),
                Category = reader.GetString(5),
                Condition = reader.GetString(6),
                Location = reader.IsDBNull(7) ? "" : reader.GetString(7),
                ImageUrls = reader.IsDBNull(8) ? "" : reader.GetString(8),
                IsAvailable = string.Equals(reader.GetString(9), "Available", StringComparison.OrdinalIgnoreCase),
                CreatedAt = reader.GetDateTime(10),
                FacebookUrl = showSocial && !reader.IsDBNull(13) ? reader.GetString(13) : "",
                InstagramUrl = showSocial && !reader.IsDBNull(14) ? reader.GetString(14) : "",
                OtherSocialUrl = showSocial && !reader.IsDBNull(15) ? reader.GetString(15) : ""
            });
        }
        return result;
    }

    private static string? ValidateListing(CreateListingRequest request)
    {
        var title = (request.Title ?? string.Empty).Trim();
        var description = (request.Description ?? string.Empty).Trim();
        var category = (request.Category ?? string.Empty).Trim();
        var condition = (request.Condition ?? string.Empty).Trim();
        var location = (request.Location ?? string.Empty).Trim();
        var imageUrls = (request.ImageUrls ?? string.Empty).Trim();

        if (request.UserId <= 0) return "A valid user ID is required.";
        if (title.Length < 2 || title.Length > 80) return "Listing title must be between 2 and 80 characters.";
        if (description.Length < 10 || description.Length > 500) return "Description must be between 10 and 500 characters.";
        if (request.Price <= 0 || request.Price > 1_000_000m) return "Price must be greater than zero and no more than 1,000,000.";

        string[] allowedCategories =
        [
            "General", "Food & Treats", "Accessories", "Medicine",
            "Cage & Beds", "Toys", "Grooming", "Other"
        ];
        if (!allowedCategories.Contains(category)) return "Marketplace category is not valid.";

        string[] allowedConditions = ["Brand New", "Like New", "Good", "Used"];
        if (!allowedConditions.Contains(condition)) return "Item condition is not valid.";

        string[] allowedLocations = ["Lipa City", "Tanauan City", "Malvar", "Sto. Tomas", "Other nearby area"];
        if (!allowedLocations.Contains(location)) return "Meetup area is not valid.";

        if (string.IsNullOrWhiteSpace(imageUrls)) return "At least one item photo is required.";

        var images = imageUrls
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (images.Length is < 1 or > 5) return "A listing must contain between 1 and 5 photos.";

        if (images.Any(x => x.Length > 2048)) return "One or more photo references are too long.";

        return null;
    }

    private static void AddListingParameters(SqlCommand cmd, int userId, string? title, string? description, decimal price, string? category, string? condition, string? location, string? imageUrls)
    {
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@Title", title ?? "");
        cmd.Parameters.AddWithValue("@Description", description ?? "");
        cmd.Parameters.AddWithValue("@Price", price);
        cmd.Parameters.AddWithValue("@Category", category ?? "General");
        cmd.Parameters.AddWithValue("@Condition", condition ?? "Used");
        cmd.Parameters.AddWithValue("@Location", location ?? "");
        cmd.Parameters.AddWithValue("@ImageUrls", (object?)imageUrls ?? DBNull.Value);
        string firstImage = string.IsNullOrWhiteSpace(imageUrls)
            ? string.Empty
            : imageUrls.Split(',', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? string.Empty;
        if (firstImage.Length > 300) firstImage = firstImage[..300];
        cmd.Parameters.AddWithValue("@ImageUrl", string.IsNullOrWhiteSpace(firstImage) ? DBNull.Value : firstImage);
    }
}

public class CreateListingRequest
{
    public int UserId { get; set; }
    public string? SellerName { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? Category { get; set; }
    public string? Condition { get; set; }
    public string? Location { get; set; }
    public string? ImageUrls { get; set; }
}
public class EditListingRequest : CreateListingRequest { }
