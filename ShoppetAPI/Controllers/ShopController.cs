using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Data;

namespace ShoppetAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ShopController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public ShopController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // ── Get Products (supports optional filtering by species, category, or search query) ──
        [HttpGet("products")]
        public async Task<IActionResult> GetProducts([FromQuery] string? species, [FromQuery] string? category, [FromQuery] string? search)
        {
            try
            {
                string connString = _configuration.GetConnectionString("DefaultConnection")!;
                var products = new List<object>();

                using (var connection = new MySqlConnection(connString))
                {
                    await connection.OpenAsync();

                    var query = @"SELECT
                                      p.Id,
                                      p.Name,
                                      p.Description,
                                      p.Price,
                                      p.ImageUrl,
                                      p.StockQuantity,
                                      p.IsAvailable,
                                      p.CreatedAt,
                                      GROUP_CONCAT(DISTINCT pc.Name ORDER BY pc.Name SEPARATOR ',') AS SpeciesNames,
                                      GROUP_CONCAT(DISTINCT sc.Name ORDER BY sc.Name SEPARATOR ',') AS CategoryNames
                                  FROM products p
                                  LEFT JOIN productpetcategories ppc ON p.Id = ppc.ProductId
                                  LEFT JOIN petcategories pc ON ppc.PetCategoryId = pc.Id
                                  LEFT JOIN productshopcategories psc ON p.Id = psc.ProductId
                                  LEFT JOIN shopcategories sc ON psc.ShopCategoryId = sc.Id
                                  WHERE 1=1";

                    var cmd = new MySqlCommand();
                    cmd.Connection = connection;

                    if (!string.IsNullOrEmpty(species))
                    {
                        query += " AND (pc.Name = @species OR pc.Name IS NULL)";
                        cmd.Parameters.AddWithValue("@species", species);
                    }

                    if (!string.IsNullOrEmpty(category))
                    {
                        query += " AND sc.Name = @category";
                        cmd.Parameters.AddWithValue("@category", category);
                    }

                    if (!string.IsNullOrEmpty(search))
                    {
                        query += " AND (p.Name LIKE @search OR p.Description LIKE @search)";
                        cmd.Parameters.AddWithValue("@search", $"%{search}%");
                    }

                    query += @" GROUP BY
                                    p.Id,
                                    p.Name,
                                    p.Description,
                                    p.Price,
                                    p.ImageUrl,
                                    p.StockQuantity,
                                    p.IsAvailable,
                                    p.CreatedAt
                                ORDER BY p.Name";

                    cmd.CommandText = query;

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            products.Add(new
                            {
                                Id = reader.GetInt32("Id"),
                                Name = reader.GetString("Name"),
                                Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? "" : reader.GetString("Description"),
                                Price = reader.GetDecimal("Price"),
                                ImageUrl = reader.IsDBNull(reader.GetOrdinal("ImageUrl")) ? "" : reader.GetString("ImageUrl"),
                                StockQuantity = reader.GetInt32("StockQuantity"),
                                IsAvailable = reader.GetBoolean("IsAvailable"),
                                CreatedAt = reader.GetDateTime("CreatedAt"),
                                Species = reader.IsDBNull(reader.GetOrdinal("SpeciesNames"))
                                    ? new List<string>()
                                    : reader.GetString("SpeciesNames")
                                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                                        .ToList(),
                                Categories = reader.IsDBNull(reader.GetOrdinal("CategoryNames"))
                                    ? new List<string>()
                                    : reader.GetString("CategoryNames")
                                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                                        .ToList()
                            });
                        }
                    }
                }

                return Ok(products);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error fetching products: {ex.Message}");
            }
        }

        // ── Get Shop Categories ──
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                string connString = _configuration.GetConnectionString("DefaultConnection")!;
                var categories = new List<object>();

                using (var connection = new MySqlConnection(connString))
                {
                    await connection.OpenAsync();
                    var query = "SELECT Id, Name, Icon FROM shopcategories";

                    using (var cmd = new MySqlCommand(query, connection))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            categories.Add(new
                            {
                                Id = reader.GetInt32("Id"),
                                Name = reader.GetString("Name"),
                                Icon = reader.IsDBNull(reader.GetOrdinal("Icon")) ? "" : reader.GetString("Icon")
                            });
                        }
                    }
                }

                return Ok(categories);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error fetching categories: {ex.Message}");
            }
        }
    }
}