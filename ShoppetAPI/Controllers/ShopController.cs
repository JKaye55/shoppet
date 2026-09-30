using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ShoppetAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ShopController : ControllerBase
{
    private readonly IConfiguration _configuration;
    public ShopController(IConfiguration configuration)=>_configuration=configuration;

    private string ConnectionString =>
        _configuration.GetConnectionString("SharedSqlServer")
        ?? throw new InvalidOperationException("SharedSqlServer connection is missing.");

    [HttpGet("products")]
    public async Task<IActionResult> GetProducts([FromQuery] string? species,[FromQuery] string? category,[FromQuery] string? search)
    {
        try
        {
            var list=new List<object>();
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();

            var conditions=new List<string>{"1=1"};
            if(!string.IsNullOrWhiteSpace(category)&&!category.Equals("All",StringComparison.OrdinalIgnoreCase))
                conditions.Add("Category=@Category");
            if(!string.IsNullOrWhiteSpace(search))
                conditions.Add("(Name LIKE @Search OR ISNULL(Description,'') LIKE @Search)");

            var sql=$"""
                SELECT Id,Name,ISNULL(Description,''),Price,ISNULL(ImageSource,''),
                       Stock,CASE WHEN Stock>0 THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END,
                       Category,ISNULL(PetSize,'All Sizes')
                FROM PetProducts
                WHERE {string.Join(" AND ",conditions)}
                ORDER BY Name;
                """;
            await using var cmd=new SqlCommand(sql,conn);
            if(!string.IsNullOrWhiteSpace(category)&&!category.Equals("All",StringComparison.OrdinalIgnoreCase))
                cmd.Parameters.AddWithValue("@Category",category);
            if(!string.IsNullOrWhiteSpace(search))
                cmd.Parameters.AddWithValue("@Search",$"%{search.Trim()}%");

            await using var r=await cmd.ExecuteReaderAsync();
            while(await r.ReadAsync())
            {
                list.Add(new{
                    Id=r.GetInt32(0),Name=r.GetString(1),Description=r.GetString(2),
                    Price=r.GetDecimal(3),ImageUrl=r.GetString(4),StockQuantity=r.GetInt32(5),
                    IsAvailable=r.GetBoolean(6),Category=r.GetString(7),
                    Categories=new[]{r.GetString(7)},
                    Species=string.IsNullOrWhiteSpace(species)?Array.Empty<string>():new[]{species}
                });
            }
            return Ok(list);
        }
        catch(Exception ex){return StatusCode(500,$"Products error: {ex.Message}");}
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        try
        {
            var list=new List<object>();
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            await using var cmd=new SqlCommand("""
                SELECT ROW_NUMBER() OVER(ORDER BY Category) AS Id,Category
                FROM (SELECT DISTINCT Category FROM PetProducts WHERE Category IS NOT NULL AND LTRIM(RTRIM(Category))<>'') x
                ORDER BY Category;
                """,conn);
            await using var r=await cmd.ExecuteReaderAsync();
            while(await r.ReadAsync())
                list.Add(new{Id=Convert.ToInt32(r.GetInt64(0)),Name=r.GetString(1),Icon=""});
            return Ok(list);
        }
        catch(Exception ex){return StatusCode(500,$"Categories error: {ex.Message}");}
    }
}
