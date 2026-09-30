using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ShoppetAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProfileController : ControllerBase
{
    private readonly IConfiguration _config;
    public ProfileController(IConfiguration config)=>_config=config;

    private string ConnectionString =>
        _config.GetConnectionString("SharedSqlServer")
        ?? throw new InvalidOperationException("SharedSqlServer connection string not found.");

    public sealed class UpdateProfileRequest
    {
        public int UserId { get; set; }
        public string FullName { get; set; }="";
        public string ProfilePictureBase64 { get; set; }="";
        public string? FacebookUrl { get; set; }
        public string? InstagramUrl { get; set; }
        public string? OtherSocialUrl { get; set; }
        public bool? ShowSocialLinksOnMarketplace { get; set; }
    }

    [HttpGet("{userId:int}")]
    public async Task<IActionResult> GetProfile(int userId)
    {
        if(userId<=0) return BadRequest("A valid user ID is required.");
        try
        {
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            await using var cmd=new SqlCommand(@"
                SELECT FullName,Email,MobileNumber,ProfilePicture,
                       FacebookUrl,InstagramUrl,OtherSocialUrl,
                       ShowSocialLinksOnMarketplace
                FROM UserAccounts
                WHERE Id=@UserId;",conn);
            cmd.Parameters.AddWithValue("@UserId",userId);
            await using var r=await cmd.ExecuteReaderAsync();
            if(!await r.ReadAsync()) return NotFound();

            return Ok(new{
                FullName=r.IsDBNull(0)?"":r.GetString(0),
                Email=r.IsDBNull(1)?"":r.GetString(1),
                MobileNumber=r.IsDBNull(2)?"":r.GetString(2),
                ProfilePicture=r.IsDBNull(3)?"":r.GetString(3),
                FacebookUrl=r.IsDBNull(4)?"":r.GetString(4),
                InstagramUrl=r.IsDBNull(5)?"":r.GetString(5),
                OtherSocialUrl=r.IsDBNull(6)?"":r.GetString(6),
                ShowSocialLinksOnMarketplace=!r.IsDBNull(7)&&r.GetBoolean(7)
            });
        }
        catch(Exception ex){ return StatusCode(500,$"Profile read error: {ex.Message}"); }
    }

    [HttpPut("update")]
    public async Task<IActionResult> UpdateProfile([FromBody]UpdateProfileRequest request)
    {
        if(request.UserId<=0) return BadRequest("A valid user ID is required.");
        if(string.IsNullOrWhiteSpace(request.FullName)) return BadRequest("Full name is required.");
        try
        {
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            await using var cmd=new SqlCommand(@"
                UPDATE UserAccounts
                SET FullName=@FullName,
                    ProfilePicture=@ProfilePicture,
                    FacebookUrl=COALESCE(@FacebookUrl,FacebookUrl),
                    InstagramUrl=COALESCE(@InstagramUrl,InstagramUrl),
                    OtherSocialUrl=COALESCE(@OtherSocialUrl,OtherSocialUrl),
                    ShowSocialLinksOnMarketplace=COALESCE(@ShowSocial,ShowSocialLinksOnMarketplace)
                WHERE Id=@UserId;",conn);
            cmd.Parameters.AddWithValue("@UserId",request.UserId);
            cmd.Parameters.AddWithValue("@FullName",request.FullName.Trim());
            cmd.Parameters.AddWithValue("@ProfilePicture",request.ProfilePictureBase64 ?? "");
            cmd.Parameters.AddWithValue("@FacebookUrl",(object?)request.FacebookUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@InstagramUrl",(object?)request.InstagramUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@OtherSocialUrl",(object?)request.OtherSocialUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ShowSocial",(object?)request.ShowSocialLinksOnMarketplace ?? DBNull.Value);

            return await cmd.ExecuteNonQueryAsync()>0
                ? Ok(new{success=true,FullName=request.FullName.Trim()})
                : NotFound("User not found.");
        }
        catch(Exception ex){ return StatusCode(500,$"Profile update error: {ex.Message}"); }
    }
}
