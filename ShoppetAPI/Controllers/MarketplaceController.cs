using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace ShoppetAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MarketplaceController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        public MarketplaceController(IConfiguration configuration) => _configuration = configuration;

        [HttpGet]
        public async Task<IActionResult> GetListings([FromQuery] string? category = null, [FromQuery] string? search = null)
        {
            try
            {
                string conn = _configuration.GetConnectionString("DefaultConnection")!;
                using var connection = new MySqlConnection(conn);
                await connection.OpenAsync();
                var conditions = new List<string> { "m.IsAvailable = 1" };
                if (!string.IsNullOrWhiteSpace(category)) conditions.Add("m.Category = @Category");
                if (!string.IsNullOrWhiteSpace(search)) conditions.Add("(m.Title LIKE @Search OR m.Description LIKE @Search OR m.Category LIKE @Search)");
                var where = "WHERE " + string.Join(" AND ", conditions);
                var query = $@"SELECT m.*, u.FullName AS SellerFullName, u.ProfilePicture AS SellerProfilePic FROM marketplacelistings m LEFT JOIN users u ON m.UserId = u.Id {where} ORDER BY m.CreatedAt DESC;";
                using var cmd = new MySqlCommand(query, connection);
                if (!string.IsNullOrWhiteSpace(category)) cmd.Parameters.AddWithValue("@Category", category);
                if (!string.IsNullOrWhiteSpace(search)) cmd.Parameters.AddWithValue("@Search", $"%{search}%");
                var listings = new List<object>();
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    listings.Add(new {
                        Id = Convert.ToInt32(reader["Id"]),
                        UserId = Convert.ToInt32(reader["UserId"]),
                        SellerName = reader["SellerFullName"] == DBNull.Value ? reader["SellerName"].ToString() : reader["SellerFullName"].ToString(),
                        SellerProfilePic = reader["SellerProfilePic"] == DBNull.Value ? string.Empty : reader["SellerProfilePic"].ToString(),
                        Title = reader["Title"].ToString(),
                        Description = reader["Description"].ToString(),
                        Price = Convert.ToDecimal(reader["Price"]),
                        Category = reader["Category"].ToString(),
                        Condition = reader["Condition_"].ToString(),
                        Location = reader["Location_"].ToString(),
                        ImageUrls = reader["ImageUrls"] == DBNull.Value ? "" : reader["ImageUrls"].ToString(),
                        IsAvailable = Convert.ToBoolean(reader["IsAvailable"]),
                        CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
                    });
                }
                return Ok(listings);
            }
            catch (Exception ex) { return StatusCode(500, $"Error: {ex.Message}"); }
        }

        [HttpGet("my/{userId}")]
        public async Task<IActionResult> GetMyListings(int userId)
        {
            try
            {
                string conn = _configuration.GetConnectionString("DefaultConnection")!;
                using var connection = new MySqlConnection(conn);
                await connection.OpenAsync();
                var query = @"SELECT m.*, u.FullName AS SellerFullName, u.ProfilePicture AS SellerProfilePic FROM marketplacelistings m LEFT JOIN users u ON m.UserId = u.Id WHERE m.UserId = @UserId ORDER BY m.CreatedAt DESC;";
                using var cmd = new MySqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@UserId", userId);
                var listings = new List<object>();
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    listings.Add(new {
                        Id = Convert.ToInt32(reader["Id"]),
                        UserId = Convert.ToInt32(reader["UserId"]),
                        SellerName = reader["SellerFullName"] == DBNull.Value ? reader["SellerName"].ToString() : reader["SellerFullName"].ToString(),
                        SellerProfilePic = reader["SellerProfilePic"] == DBNull.Value ? string.Empty : reader["SellerProfilePic"].ToString(),
                        Title = reader["Title"].ToString(),
                        Description = reader["Description"].ToString(),
                        Price = Convert.ToDecimal(reader["Price"]),
                        Category = reader["Category"].ToString(),
                        Condition = reader["Condition_"].ToString(),
                        Location = reader["Location_"].ToString(),
                        ImageUrls = reader["ImageUrls"] == DBNull.Value ? "" : reader["ImageUrls"].ToString(),
                        IsAvailable = Convert.ToBoolean(reader["IsAvailable"]),
                        CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
                    });
                }
                return Ok(listings);
            }
            catch (Exception ex) { return StatusCode(500, $"Error: {ex.Message}"); }
        }

        [HttpPost]
        public async Task<IActionResult> CreateListing([FromBody] CreateListingRequest request)
        {
            try
            {
                string conn = _configuration.GetConnectionString("DefaultConnection")!;
                using var connection = new MySqlConnection(conn);
                await connection.OpenAsync();
                var query = @"INSERT INTO marketplacelistings (UserId, SellerName, Title, Description, Price, Category, Condition_, Location_, ImageUrls, IsAvailable, CreatedAt, UpdatedAt) VALUES (@UserId, @SellerName, @Title, @Description, @Price, @Category, @Condition, @Location, @ImageUrls, 1, NOW(), NOW()); SELECT LAST_INSERT_ID();";
                using var cmd = new MySqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@UserId", request.UserId);
                cmd.Parameters.AddWithValue("@SellerName", request.SellerName ?? "");
                cmd.Parameters.AddWithValue("@Title", request.Title ?? "");
                cmd.Parameters.AddWithValue("@Description", request.Description ?? "");
                cmd.Parameters.AddWithValue("@Price", request.Price);
                cmd.Parameters.AddWithValue("@Category", request.Category ?? "General");
                cmd.Parameters.AddWithValue("@Condition", request.Condition ?? "Used");
                cmd.Parameters.AddWithValue("@Location", request.Location ?? "");
                cmd.Parameters.AddWithValue("@ImageUrls", (object?)request.ImageUrls ?? DBNull.Value);
                var newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                return Ok(new { success = true, id = newId });
            }
            catch (Exception ex) { return StatusCode(500, $"Error: {ex.Message}"); }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> EditListing(int id, [FromBody] EditListingRequest request)
        {
            try
            {
                string conn = _configuration.GetConnectionString("DefaultConnection")!;
                using var connection = new MySqlConnection(conn);
                await connection.OpenAsync();
                var query = @"UPDATE marketplacelistings
                              SET Title=@Title,
                                  Description=@Description,
                                  Price=@Price,
                                  Category=@Category,
                                  Condition_=@Condition,
                                  Location_=@Location,
                                  ImageUrls=@ImageUrls,
                                  UpdatedAt=NOW()
                              WHERE Id=@Id AND UserId=@UserId;";
                using var cmd = new MySqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@UserId", request.UserId);
                cmd.Parameters.AddWithValue("@Title", request.Title ?? "");
                cmd.Parameters.AddWithValue("@Description", request.Description ?? "");
                cmd.Parameters.AddWithValue("@Price", request.Price);
                cmd.Parameters.AddWithValue("@Category", request.Category ?? "General");
                cmd.Parameters.AddWithValue("@Condition", request.Condition ?? "Used");
                cmd.Parameters.AddWithValue("@Location", request.Location ?? "");
                cmd.Parameters.AddWithValue("@ImageUrls", (object?)request.ImageUrls ?? DBNull.Value);
                int rows = await cmd.ExecuteNonQueryAsync();

                if (rows == 0)
                    return NotFound("Listing not found.");

                return Ok(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, $"Error: {ex.Message}"); }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteListing(int id, [FromQuery] int userId)
        {
            try
            {
                string conn = _configuration.GetConnectionString("DefaultConnection")!;
                using var connection = new MySqlConnection(conn);
                await connection.OpenAsync();
                var query = "DELETE FROM marketplacelistings WHERE Id=@Id AND UserId=@UserId;";
                using var cmd = new MySqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@UserId", userId);
                int rows = await cmd.ExecuteNonQueryAsync();

                if (rows == 0)
                    return NotFound("Listing not found.");

                return Ok(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, $"Error: {ex.Message}"); }
        }
    }

    public class CreateListingRequest { public int UserId { get; set; } public string? SellerName { get; set; } public string? Title { get; set; } public string? Description { get; set; } public decimal Price { get; set; } public string? Category { get; set; } public string? Condition { get; set; } public string? Location { get; set; } public string? ImageUrls { get; set; } }
    public class EditListingRequest
    {
        public int UserId { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string? Category { get; set; }
        public string? Condition { get; set; }
        public string? Location { get; set; }
        public string? ImageUrls { get; set; }
    }
}
