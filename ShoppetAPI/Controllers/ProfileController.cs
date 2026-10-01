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
        ?? throw new InvalidOperationException("SharedSqlServer connection is missing.");

    public class UpdateProfileRequest
    {
        public int UserId{get;set;}
        public string FullName{get;set;}=string.Empty;
        public string ProfilePictureBase64{get;set;}=string.Empty;
        public string FacebookUrl{get;set;}=string.Empty;
        public string InstagramUrl{get;set;}=string.Empty;
        public string OtherSocialUrl{get;set;}=string.Empty;
        public bool ShowSocialLinksOnMarketplace{get;set;}=true;
        public string MobileNumber{get;set;}=string.Empty;
        public bool ShowMobileOnPublicPetId{get;set;}
    }

    [HttpGet("{userId:int}")]
    public async Task<IActionResult> GetProfile(int userId)
    {
        try
        {
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            await using var cmd=new SqlCommand("""
                SELECT FullName,ISNULL(ProfilePicture,''),
                       ISNULL(FacebookUrl,''),ISNULL(InstagramUrl,''),
                       ISNULL(OtherSocialUrl,''),ISNULL(ShowSocialLinksOnMarketplace,1),
                       ISNULL(MobileNumber,''),ISNULL(ShowMobileOnPublicPetId,0)
                FROM UserAccounts WHERE Id=@UserId;
                """,conn);
            cmd.Parameters.AddWithValue("@UserId",userId);
            await using var r=await cmd.ExecuteReaderAsync();
            if(!await r.ReadAsync())return NotFound();
            return Ok(new{
                FullName=r.GetString(0),
                ProfilePicture=r.GetString(1),
                FacebookUrl=r.GetString(2),
                InstagramUrl=r.GetString(3),
                OtherSocialUrl=r.GetString(4),
                ShowSocialLinksOnMarketplace=r.GetBoolean(5),
                MobileNumber=r.GetString(6),
                ShowMobileOnPublicPetId=r.GetBoolean(7)
            });
        }
        catch(Exception ex){return StatusCode(500,$"Profile error: {ex.Message}");}
    }

    [HttpPut("update")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req)
    {
        if(req.UserId<=0||string.IsNullOrWhiteSpace(req.FullName))
            return BadRequest("A valid user and full name are required.");
        foreach(var url in new[]{req.FacebookUrl,req.InstagramUrl,req.OtherSocialUrl})
            if(!string.IsNullOrWhiteSpace(url)&&(!Uri.TryCreate(url,UriKind.Absolute,out var parsed)||parsed.Scheme!="https"||url.Length>300))return BadRequest("Social links must be complete HTTPS URLs.");
        try
        {
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            await using var cmd=new SqlCommand("""
                UPDATE UserAccounts
                SET FullName=@FullName,
                    ProfilePicture=@ProfilePicture,
                    FacebookUrl=@FacebookUrl,
                    InstagramUrl=@InstagramUrl,
                    OtherSocialUrl=@OtherSocialUrl,
                    ShowSocialLinksOnMarketplace=@ShowSocialLinksOnMarketplace,
                    MobileNumber=@MobileNumber,
                    ShowMobileOnPublicPetId=@ShowMobileOnPublicPetId
                WHERE Id=@UserId;
                """,conn);
            cmd.Parameters.AddWithValue("@UserId",req.UserId);
            cmd.Parameters.AddWithValue("@FullName",req.FullName.Trim());
            cmd.Parameters.AddWithValue("@ProfilePicture",req.ProfilePictureBase64??string.Empty);
            cmd.Parameters.AddWithValue("@FacebookUrl",req.FacebookUrl??string.Empty);
            cmd.Parameters.AddWithValue("@InstagramUrl",req.InstagramUrl??string.Empty);
            cmd.Parameters.AddWithValue("@OtherSocialUrl",req.OtherSocialUrl??string.Empty);
            cmd.Parameters.AddWithValue("@ShowSocialLinksOnMarketplace",req.ShowSocialLinksOnMarketplace);
            cmd.Parameters.AddWithValue("@MobileNumber",req.MobileNumber??string.Empty);
            cmd.Parameters.AddWithValue("@ShowMobileOnPublicPetId",req.ShowMobileOnPublicPetId);
            return await cmd.ExecuteNonQueryAsync()==0?NotFound():Ok(new{success=true});
        }
        catch(Exception ex){return StatusCode(500,$"Profile update error: {ex.Message}");}
    }
}
