using ShoppetAPI.Security;
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
        public string? MobileNumber { get; set; }
        public string? FacebookUrl { get; set; }
        public string? InstagramUrl { get; set; }
        public string? OtherSocialUrl { get; set; }
        public bool? ShowSocialLinksOnMarketplace { get; set; }
        public bool? ShowMobileOnPublicPetId { get; set; }
    }

    [HttpGet("{userId:int}")]
    public async Task<IActionResult> GetProfile(int userId)
    {
        if(userId<=0) return BadRequest("A valid user ID is required.");
        if(!this.IsAuthenticatedUser(userId)) return Forbid();
        try
        {
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            await using var cmd=new SqlCommand(@"
                SELECT FullName,Email,MobileNumber,ProfilePicture,
                       FacebookUrl,InstagramUrl,OtherSocialUrl,
                       ShowSocialLinksOnMarketplace,ShowMobileOnPublicPetId
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
                ShowSocialLinksOnMarketplace=!r.IsDBNull(7)&&r.GetBoolean(7),
                ShowMobileOnPublicPetId=!r.IsDBNull(8)&&r.GetBoolean(8)
            });
        }
        catch(Exception ex){ return StatusCode(500,$"Profile read error: {ex.Message}"); }
    }

    [HttpPut("update")]
    public async Task<IActionResult> UpdateProfile([FromBody]UpdateProfileRequest request)
    {
        if(request.UserId<=0) return BadRequest("A valid user ID is required.");
        if(!this.IsAuthenticatedUser(request.UserId)) return Forbid();

        var fullName=(request.FullName??string.Empty).Trim();
        if(fullName.Length<2||fullName.Length>80) return BadRequest("Full name must be between 2 and 80 characters.");

        var mobile=(request.MobileNumber??string.Empty).Trim();
        if(!string.IsNullOrWhiteSpace(mobile) &&
           !(mobile.Length==11 && mobile.StartsWith("09") && mobile.All(char.IsDigit)))
            return BadRequest("Mobile number must use the Philippine 09XXXXXXXXX format.");

        static bool IsValidOptionalUrl(string? value)
        {
            if(string.IsNullOrWhiteSpace(value)) return true;
            if(value.Length>500) return false;
            return Uri.TryCreate(value.Trim(),UriKind.Absolute,out var uri)
                && (uri.Scheme==Uri.UriSchemeHttps||uri.Scheme==Uri.UriSchemeHttp);
        }

        if(!IsValidOptionalUrl(request.FacebookUrl) ||
           !IsValidOptionalUrl(request.InstagramUrl) ||
           !IsValidOptionalUrl(request.OtherSocialUrl))
            return BadRequest("Social links must be valid HTTP or HTTPS web addresses.");

        try
        {
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            await using var cmd=new SqlCommand(@"
                UPDATE UserAccounts
                SET FullName=@FullName,
                    MobileNumber=@MobileNumber,
                    ProfilePicture=@ProfilePicture,
                    FacebookUrl=@FacebookUrl,
                    InstagramUrl=@InstagramUrl,
                    OtherSocialUrl=@OtherSocialUrl,
                    ShowSocialLinksOnMarketplace=@ShowSocial,
                    ShowMobileOnPublicPetId=@ShowMobile
                WHERE Id=@UserId;",conn);
            cmd.Parameters.AddWithValue("@UserId",request.UserId);
            cmd.Parameters.AddWithValue("@FullName",fullName);
            cmd.Parameters.AddWithValue("@MobileNumber",string.IsNullOrWhiteSpace(mobile)?DBNull.Value:mobile);
            cmd.Parameters.AddWithValue("@ProfilePicture",request.ProfilePictureBase64 ?? "");
            cmd.Parameters.AddWithValue("@FacebookUrl",string.IsNullOrWhiteSpace(request.FacebookUrl)?DBNull.Value:request.FacebookUrl.Trim());
            cmd.Parameters.AddWithValue("@InstagramUrl",string.IsNullOrWhiteSpace(request.InstagramUrl)?DBNull.Value:request.InstagramUrl.Trim());
            cmd.Parameters.AddWithValue("@OtherSocialUrl",string.IsNullOrWhiteSpace(request.OtherSocialUrl)?DBNull.Value:request.OtherSocialUrl.Trim());
            cmd.Parameters.AddWithValue("@ShowSocial",request.ShowSocialLinksOnMarketplace ?? false);
            cmd.Parameters.AddWithValue("@ShowMobile",request.ShowMobileOnPublicPetId ?? false);

            return await cmd.ExecuteNonQueryAsync()>0
                ? Ok(new{success=true,FullName=fullName})
                : NotFound("User not found.");
        }
        catch(Exception ex){ return StatusCode(500,$"Profile update error: {ex.Message}"); }
    }
}
