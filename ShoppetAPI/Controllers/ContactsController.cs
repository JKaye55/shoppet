using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using ShoppetAPI.Services;

namespace ShoppetAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ContactsController : ControllerBase
{
    private readonly IConfiguration _configuration;
    public ContactsController(IConfiguration configuration)=>_configuration=configuration;

    private string ConnectionString =>
        _configuration.GetConnectionString("SharedSqlServer")
        ?? throw new InvalidOperationException("SharedSqlServer connection is missing.");

    [HttpGet]
    public async Task<IActionResult> GetContacts([FromQuery] int userId)
    {
        try
        {
            var list=new List<object>();
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();

            if(!await RbacService.IsPetOwnerAsync(conn,userId))
                return StatusCode(403,"Access denied.");

            var sql="""
                SELECT Id,UserId,Name,ISNULL(Role,''),ISNULL(Address,''),
                       ISNULL(Phone,''),IsEmergency
                FROM EmergencyContacts
                WHERE UserId=@UserId
                ORDER BY IsEmergency DESC, Name;
                """;

            await using var cmd=new SqlCommand(sql,conn);
            cmd.Parameters.AddWithValue("@UserId",userId);

            await using var r=await cmd.ExecuteReaderAsync();
            while(await r.ReadAsync())
            {
                list.Add(new{
                    Id=r.GetInt32(0),UserId=r.GetInt32(1),Name=r.GetString(2),
                    Role=r.GetString(3),Address=r.GetString(4),Phone=r.GetString(5),
                    IsEmergency=r.GetBoolean(6)
                });
            }
            return Ok(list);
        }
        catch(Exception ex){return StatusCode(500,$"Contacts error: {ex.Message}");}
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ContactRequest x)
    {
        if(x.UserId<=0||string.IsNullOrWhiteSpace(x.Name))
            return BadRequest("A valid user and contact name are required.");
        try
        {
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();

            if(!await RbacService.IsPetOwnerAsync(conn,x.UserId))
                return StatusCode(403,"Access denied.");

            await using var cmd=new SqlCommand("""
                INSERT INTO EmergencyContacts(UserId,Name,Role,Address,Phone,IsEmergency)
                OUTPUT INSERTED.Id
                VALUES(@UserId,@Name,@Role,@Address,@Phone,@IsEmergency);
                """,conn);
            Bind(cmd,x);
            var id=Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return Ok(new{Id=id,x.UserId,Name=x.Name.Trim(),x.Role,x.Address,x.Phone,x.IsEmergency});
        }
        catch(Exception ex){return StatusCode(500,$"Contact save error: {ex.Message}");}
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id,[FromBody] ContactRequest x)
    {
        try
        {
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();

            if(!await RbacService.IsPetOwnerAsync(conn,x.UserId))
                return StatusCode(403,"Access denied.");

            await using var cmd=new SqlCommand("""
                UPDATE EmergencyContacts
                SET Name=@Name,Role=@Role,Address=@Address,Phone=@Phone,IsEmergency=@IsEmergency
                WHERE Id=@Id AND UserId=@UserId;
                """,conn);
            Bind(cmd,x);
            cmd.Parameters.AddWithValue("@Id",id);
            return await cmd.ExecuteNonQueryAsync()==0?NotFound():Ok(new{success=true});
        }
        catch(Exception ex){return StatusCode(500,$"Contact update error: {ex.Message}");}
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id,[FromQuery] int userId)
    {
        try
        {
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            if(!await RbacService.IsPetOwnerAsync(conn,userId))
                return StatusCode(403,"Access denied.");

            const string sql="DELETE FROM EmergencyContacts WHERE Id=@Id AND UserId=@UserId";
            await using var cmd=new SqlCommand(sql,conn);
            cmd.Parameters.AddWithValue("@Id",id);
            cmd.Parameters.AddWithValue("@UserId",userId);
            return await cmd.ExecuteNonQueryAsync()==0?NotFound():Ok(new{success=true});
        }
        catch(Exception ex){return StatusCode(500,$"Contact delete error: {ex.Message}");}
    }

    private static void Bind(SqlCommand cmd,ContactRequest x)
    {
        cmd.Parameters.AddWithValue("@UserId",x.UserId);
        cmd.Parameters.AddWithValue("@Name",x.Name?.Trim()??string.Empty);
        cmd.Parameters.AddWithValue("@Role",x.Role?.Trim()??string.Empty);
        cmd.Parameters.AddWithValue("@Address",x.Address?.Trim()??string.Empty);
        cmd.Parameters.AddWithValue("@Phone",x.Phone?.Trim()??string.Empty);
        cmd.Parameters.AddWithValue("@IsEmergency",x.IsEmergency);
    }
}
public class ContactRequest
{
    public int UserId{get;set;}
    public string Name{get;set;}=string.Empty;
    public string Role{get;set;}=string.Empty;
    public string Address{get;set;}=string.Empty;
    public string Phone{get;set;}=string.Empty;
    public bool IsEmergency{get;set;}
}
