using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ShoppetAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ShopController : ControllerBase
{
    private readonly IConfiguration _configuration;
    public ShopController(IConfiguration configuration) => _configuration = configuration;

    private string ConnectionString =>
        _configuration.GetConnectionString("SharedSqlServer")
        ?? throw new InvalidOperationException("SharedSqlServer connection string not found.");

    [HttpGet("products")]
    public async Task<IActionResult> GetProducts([FromQuery] string? species,[FromQuery] string? category,[FromQuery] string? search)
    {
        try
        {
            var products=new List<object>();
            await using var connection=new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            string sql=@"
                SELECT p.Id,p.Name,p.Description,p.Price,p.ImageUrl,p.StockQuantity,p.IsAvailable,p.CreatedAt
                FROM ShopProducts p
                WHERE (@Search IS NULL OR p.Name LIKE @SearchLike OR p.Description LIKE @SearchLike)
                  AND (@Category IS NULL OR EXISTS(
                        SELECT 1 FROM ProductShopCategories psc
                        JOIN ShopCategories sc ON sc.Id=psc.ShopCategoryId
                        WHERE psc.ProductId=p.Id AND sc.Name=@Category))
                  AND (@Species IS NULL OR EXISTS(
                        SELECT 1 FROM ProductPetCategories ppc
                        JOIN PetCategories pc ON pc.Id=ppc.PetCategoryId
                        WHERE ppc.ProductId=p.Id AND pc.Name=@Species))
                ORDER BY p.Name;";

            await using var cmd=new SqlCommand(sql,connection);
            cmd.Parameters.AddWithValue("@Search",(object?)search ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SearchLike",string.IsNullOrWhiteSpace(search)?DBNull.Value:$"%{search}%");
            cmd.Parameters.AddWithValue("@Category",(object?)category ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Species",(object?)species ?? DBNull.Value);

            await using var reader=await cmd.ExecuteReaderAsync();
            while(await reader.ReadAsync())
            {
                int id=reader.GetInt32(0);
                products.Add(new{
                    Id=id,
                    Name=reader.GetString(1),
                    Description=reader.GetString(2),
                    Price=reader.GetDecimal(3),
                    ImageUrl=reader.IsDBNull(4)?"":reader.GetString(4),
                    StockQuantity=reader.GetInt32(5),
                    IsAvailable=reader.GetBoolean(6),
                    CreatedAt=reader.GetDateTime(7),
                    Species=await GetNamesAsync(connection,
                        @"SELECT pc.Name FROM ProductPetCategories x JOIN PetCategories pc ON pc.Id=x.PetCategoryId WHERE x.ProductId=@Id",id),
                    Categories=await GetNamesAsync(connection,
                        @"SELECT sc.Name FROM ProductShopCategories x JOIN ShopCategories sc ON sc.Id=x.ShopCategoryId WHERE x.ProductId=@Id",id)
                });
            }
            return Ok(products);
        }
        catch(Exception ex){ return StatusCode(500,$"Error fetching products: {ex.Message}"); }
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        try
        {
            var result=new List<object>();
            await using var connection=new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var cmd=new SqlCommand("SELECT Id,Name,Icon FROM ShopCategories ORDER BY Name",connection);
            await using var reader=await cmd.ExecuteReaderAsync();
            while(await reader.ReadAsync()) result.Add(new{Id=reader.GetInt32(0),Name=reader.GetString(1),Icon=reader.GetString(2)});
            return Ok(result);
        }
        catch(Exception ex){ return StatusCode(500,$"Error fetching categories: {ex.Message}"); }
    }

    private static async Task<List<string>> GetNamesAsync(SqlConnection connection,string sql,int id)
    {
        var names=new List<string>();
        await using var cmd=new SqlCommand(sql,connection);
        cmd.Parameters.AddWithValue("@Id",id);
        await using var reader=await cmd.ExecuteReaderAsync();
        while(await reader.ReadAsync()) names.Add(reader.GetString(0));
        return names;
    }
}
