using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using ShoppetAPI.Services;

namespace ShoppetAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MarketplaceController : ControllerBase
{
    private readonly IConfiguration _configuration;
    public MarketplaceController(IConfiguration configuration)=>_configuration=configuration;

    private string ConnectionString =>
        _configuration.GetConnectionString("SharedSqlServer")
        ?? throw new InvalidOperationException("SharedSqlServer connection is missing.");

    [HttpGet]
    public async Task<IActionResult> GetListings([FromQuery] string? category=null,[FromQuery] string? search=null)
    {
        try
        {
            var list=new List<object>();
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();

            var conditions=new List<string>{"ISNULL(m.Status,'Available') IN ('Available','Active')"};
            if(!string.IsNullOrWhiteSpace(category)&&!category.Equals("All",StringComparison.OrdinalIgnoreCase))
                conditions.Add("m.Category=@Category");
            if(!string.IsNullOrWhiteSpace(search))
                conditions.Add("(m.Title LIKE @Search OR ISNULL(m.Description,'') LIKE @Search OR m.Category LIKE @Search)");

            var sql=$"""
                SELECT m.Id,m.SellerUserId,ISNULL(u.FullName,'Unknown'),
                       ISNULL(u.ProfilePicture,''),m.Title,ISNULL(m.Description,''),
                       m.Price,m.Category,ISNULL(m.ItemCondition,'Used'),
                       ISNULL(m.Location,''),ISNULL(m.ImageUrl,''),
                       CASE WHEN ISNULL(m.Status,'Available') IN('Available','Active') THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END,
                       m.CreatedAt,
                       CASE WHEN ISNULL(u.ShowSocialLinksOnMarketplace,1)=1 THEN ISNULL(u.FacebookUrl,'') ELSE '' END,
                       CASE WHEN ISNULL(u.ShowSocialLinksOnMarketplace,1)=1 THEN ISNULL(u.InstagramUrl,'') ELSE '' END,
                       CASE WHEN ISNULL(u.ShowSocialLinksOnMarketplace,1)=1 THEN ISNULL(u.OtherSocialUrl,'') ELSE '' END
                FROM MarketplaceListings m
                LEFT JOIN UserAccounts u ON u.Id=m.SellerUserId
                WHERE {string.Join(" AND ",conditions)}
                ORDER BY m.CreatedAt DESC;
                """;

            await using var cmd=new SqlCommand(sql,conn);
            if(!string.IsNullOrWhiteSpace(category)&&!category.Equals("All",StringComparison.OrdinalIgnoreCase))
                cmd.Parameters.AddWithValue("@Category",category);
            if(!string.IsNullOrWhiteSpace(search))
                cmd.Parameters.AddWithValue("@Search",$"%{search.Trim()}%");

            await using var r=await cmd.ExecuteReaderAsync();
            while(await r.ReadAsync()) list.Add(Map(r));
            return Ok(list);
        }
        catch(Exception ex){return StatusCode(500,$"Marketplace error: {ex.Message}");}
    }

    [HttpGet("my/{userId:int}")]
    public async Task<IActionResult> GetMyListings(int userId)
    {
        try
        {
            var list=new List<object>();
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            await using var cmd=new SqlCommand("""
                SELECT m.Id,m.SellerUserId,ISNULL(u.FullName,'Unknown'),
                       ISNULL(u.ProfilePicture,''),m.Title,ISNULL(m.Description,''),
                       m.Price,m.Category,ISNULL(m.ItemCondition,'Used'),
                       ISNULL(m.Location,''),ISNULL(m.ImageUrl,''),
                       CASE WHEN ISNULL(m.Status,'Available') IN('Available','Active') THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END,
                       m.CreatedAt,
                       CASE WHEN ISNULL(u.ShowSocialLinksOnMarketplace,1)=1 THEN ISNULL(u.FacebookUrl,'') ELSE '' END,
                       CASE WHEN ISNULL(u.ShowSocialLinksOnMarketplace,1)=1 THEN ISNULL(u.InstagramUrl,'') ELSE '' END,
                       CASE WHEN ISNULL(u.ShowSocialLinksOnMarketplace,1)=1 THEN ISNULL(u.OtherSocialUrl,'') ELSE '' END
                FROM MarketplaceListings m
                LEFT JOIN UserAccounts u ON u.Id=m.SellerUserId
                WHERE m.SellerUserId=@UserId AND m.Status<>'Deleted'
                ORDER BY m.CreatedAt DESC;
                """,conn);
            cmd.Parameters.AddWithValue("@UserId",userId);
            await using var r=await cmd.ExecuteReaderAsync();
            while(await r.ReadAsync()) list.Add(Map(r));
            return Ok(list);
        }
        catch(Exception ex){return StatusCode(500,$"Marketplace error: {ex.Message}");}
    }

    [HttpPost]
    public async Task<IActionResult> CreateListing([FromBody] CreateListingRequest x)
    {
        if(x.UserId<=0||string.IsNullOrWhiteSpace(x.Title)||x.Title.Length>150||x.Price<=0)
            return BadRequest("A valid seller and title are required.");
        try
        {
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();

            if(!await RbacService.IsPetOwnerAsync(conn,x.UserId))
                return StatusCode(403,"Access denied.");

            await using var cmd=new SqlCommand("""
                INSERT INTO MarketplaceListings
                (SellerUserId,Title,Category,ItemCondition,Price,Description,Location,ImageUrl,Status,CreatedAt)
                OUTPUT INSERTED.Id
                VALUES(@UserId,@Title,@Category,@Condition,@Price,@Description,@Location,@ImageUrl,'Available',SYSDATETIME());
                """,conn);
            Bind(cmd,x);
            var id=Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(new{success=true,id});
        }
        catch(Exception ex){return StatusCode(500,$"Listing save error: {ex.Message}");}
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> EditListing(int id,[FromBody] EditListingRequest x)
    {
        if(string.IsNullOrWhiteSpace(x.Title)||x.Title.Length>150||x.Price<=0)return BadRequest("Enter a title and positive price.");
        try
        {
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();

            if(!await RbacService.IsPetOwnerAsync(conn,x.UserId))
                return StatusCode(403,"Access denied.");

            await using var cmd=new SqlCommand("""
                UPDATE MarketplaceListings
                SET Title=@Title,Description=@Description,Price=@Price,Category=@Category,
                    ItemCondition=@Condition,Location=@Location,ImageUrl=@ImageUrl
                WHERE Id=@Id AND SellerUserId=@UserId;
                """,conn);
            cmd.Parameters.AddWithValue("@Id",id);
            cmd.Parameters.AddWithValue("@UserId",x.UserId);
            cmd.Parameters.AddWithValue("@Title",x.Title??string.Empty);
            cmd.Parameters.AddWithValue("@Description",x.Description??string.Empty);
            cmd.Parameters.AddWithValue("@Price",x.Price);
            cmd.Parameters.AddWithValue("@Category",x.Category??"General");
            cmd.Parameters.AddWithValue("@Condition",x.Condition??"Used");
            cmd.Parameters.AddWithValue("@Location",x.Location??string.Empty);
            cmd.Parameters.AddWithValue("@ImageUrl",MediaUrls.Canonical(x.ImageUrls));
            return await cmd.ExecuteNonQueryAsync()==0?NotFound():Ok(new{success=true});
        }
        catch(Exception ex){return StatusCode(500,$"Listing update error: {ex.Message}");}
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteListing(int id,[FromQuery] int userId)
    {
        try
        {
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();

            var role = await RbacService.GetActiveRoleAsync(conn,userId);
            if(role is null) return Unauthorized();

            var sql = RbacService.IsAdmin(role)
                ? "UPDATE MarketplaceListings SET Status='Deleted',UpdatedAt=SYSDATETIME() WHERE Id=@Id"
                : "UPDATE MarketplaceListings SET Status='Deleted',UpdatedAt=SYSDATETIME() WHERE Id=@Id AND SellerUserId=@UserId";

            await using var cmd=new SqlCommand(sql,conn);
            cmd.Parameters.AddWithValue("@Id",id);
            cmd.Parameters.AddWithValue("@UserId",userId);
            return await cmd.ExecuteNonQueryAsync()==0?NotFound():Ok(new{success=true});
        }
        catch(Exception ex){return StatusCode(500,$"Listing delete error: {ex.Message}");}
    }

    private object Map(SqlDataReader r)=>new{
        Id=r.GetInt32(0),UserId=r.GetInt32(1),SellerName=r.GetString(2),
        SellerProfilePic=r.GetString(3),Title=r.GetString(4),Description=r.GetString(5),
        Price=r.GetDecimal(6),Category=r.GetString(7),Condition=r.GetString(8),
        Location=r.GetString(9),ImageUrls=MediaUrl(r.GetString(10)),IsAvailable=r.GetBoolean(11),
        CreatedAt=r.GetDateTime(12),FacebookUrl=Social(r.GetString(13)),
        InstagramUrl=Social(r.GetString(14)),OtherSocialUrl=Social(r.GetString(15))
    };

    private string MediaUrl(string value)=>string.Join(",",value.Split(',',StringSplitOptions.RemoveEmptyEntries).Select(u=>u.StartsWith("/")?(_configuration["PublicWebBaseUrl"]??"http://localhost:5253").TrimEnd('/')+u:u));
    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> SetStatus(int id,[FromQuery]int userId,[FromQuery]string status)
    {
        if(!new[]{"Available","Sold","Unavailable"}.Contains(status))return BadRequest("Invalid listing status.");
        await using var c=new SqlConnection(ConnectionString);await c.OpenAsync();
        await using var q=new SqlCommand("UPDATE MarketplaceListings SET Status=@Status,UpdatedAt=SYSDATETIME() WHERE Id=@Id AND SellerUserId=@U AND Status<>'Deleted' AND (@Status<>'Available' OR NOT EXISTS(SELECT 1 FROM MarketplaceOrderItems WHERE ListingId=@Id))",c);
        q.Parameters.AddWithValue("@Id",id);q.Parameters.AddWithValue("@U",userId);q.Parameters.AddWithValue("@Status",status);
        return await q.ExecuteNonQueryAsync()>0?Ok(new{success=true}):BadRequest("Purchased listings cannot be reopened.");
    }
    private static string Social(string url)=>Uri.TryCreate(url,UriKind.Absolute,out var u)&&u.Scheme=="https"?url:"";
    private static void Bind(SqlCommand cmd,CreateListingRequest x)
    {
        cmd.Parameters.AddWithValue("@UserId",x.UserId);
        cmd.Parameters.AddWithValue("@Title",x.Title??string.Empty);
        cmd.Parameters.AddWithValue("@Category",x.Category??"General");
        cmd.Parameters.AddWithValue("@Condition",x.Condition??"Used");
        cmd.Parameters.AddWithValue("@Price",x.Price);
        cmd.Parameters.AddWithValue("@Description",x.Description??string.Empty);
        cmd.Parameters.AddWithValue("@Location",x.Location??string.Empty);
        cmd.Parameters.AddWithValue("@ImageUrl",MediaUrls.Canonical(x.ImageUrls));
    }
}
public class CreateListingRequest
{
    public int UserId{get;set;}
    public string? SellerName{get;set;}
    public string? Title{get;set;}
    public string? Description{get;set;}
    public decimal Price{get;set;}
    public string? Category{get;set;}
    public string? Condition{get;set;}
    public string? Location{get;set;}
    public string? ImageUrls{get;set;}
}
public class EditListingRequest
{
    public int UserId{get;set;}
    public string? Title{get;set;}
    public string? Description{get;set;}
    public decimal Price{get;set;}
    public string? Category{get;set;}
    public string? Condition{get;set;}
    public string? Location{get;set;}
    public string? ImageUrls{get;set;}
}
