using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
namespace ShoppetAPI.Controllers;
[ApiController,Route("api/vetvisits")]
public class VetVisitsController(IConfiguration configuration):ControllerBase
{
 string ConnectionString=>configuration.GetConnectionString("SharedSqlServer")!;
 [HttpGet] public async Task<IActionResult> Get([FromQuery]int userId)
 {
  await using var c=new SqlConnection(ConnectionString);await c.OpenAsync();await using var q=new SqlCommand("SELECT Id,PetId,ISNULL(ClinicName,''),VisitAt,ISNULL(Purpose,''),ISNULL(Notes,''),Completed FROM VetVisitReminders WHERE UserId=@U ORDER BY VisitAt",c);q.Parameters.AddWithValue("@U",userId);await using var r=await q.ExecuteReaderAsync();var rows=new List<object>();while(await r.ReadAsync())rows.Add(new{Id=r.GetInt32(0),UserId=userId,PetId=r.GetInt32(1),ClinicName=r.GetString(2),VisitAt=r.GetDateTime(3),Purpose=r.GetString(4),Notes=r.GetString(5),Completed=r.GetBoolean(6)});return Ok(rows);
 }
 [HttpPost] public Task<IActionResult> Create([FromBody]VisitRequest request)=>Save(0,request);
 [HttpPut("{id:int}")] public Task<IActionResult> Update(int id,[FromBody]VisitRequest request)=>Save(id,request);
 async Task<IActionResult> Save(int id,VisitRequest x)
 {
  if(x.PetId<=0||x.ClinicName.Length>150||x.Purpose.Length>150||x.Notes.Length>1000)return BadRequest("Choose a pet and enter valid reminder details.");
  await using var c=new SqlConnection(ConnectionString);await c.OpenAsync();await using var q=new SqlCommand(id==0?"INSERT INTO VetVisitReminders(UserId,PetId,ClinicName,VisitAt,Purpose,Notes,Completed) OUTPUT INSERTED.Id VALUES(@U,@Pet,@Clinic,@At,@Purpose,@Notes,@Done)":"UPDATE VetVisitReminders SET PetId=@Pet,ClinicName=@Clinic,VisitAt=@At,Purpose=@Purpose,Notes=@Notes,Completed=@Done OUTPUT INSERTED.Id WHERE Id=@Id AND UserId=@U",c);q.Parameters.AddWithValue("@U",x.UserId);q.Parameters.AddWithValue("@Pet",x.PetId);q.Parameters.AddWithValue("@Clinic",x.ClinicName);q.Parameters.AddWithValue("@At",x.VisitAt);q.Parameters.AddWithValue("@Purpose",x.Purpose);q.Parameters.AddWithValue("@Notes",x.Notes);q.Parameters.AddWithValue("@Done",x.Completed);q.Parameters.AddWithValue("@Id",id);var result=await q.ExecuteScalarAsync();return result is null?NotFound():Ok(new{Id=Convert.ToInt32(result)});
 }
 [HttpDelete("{id:int}")]public async Task<IActionResult> Delete(int id,[FromQuery]int userId){await using var c=new SqlConnection(ConnectionString);await c.OpenAsync();await using var q=new SqlCommand("DELETE FROM VetVisitReminders WHERE Id=@Id AND UserId=@U",c);q.Parameters.AddWithValue("@Id",id);q.Parameters.AddWithValue("@U",userId);return await q.ExecuteNonQueryAsync()>0?Ok():NotFound();}
}
public class VisitRequest{public int UserId{get;set;}public int PetId{get;set;}public string ClinicName{get;set;}="";public DateTime VisitAt{get;set;}=DateTime.Now.AddDays(1);public string Purpose{get;set;}="";public string Notes{get;set;}="";public bool Completed{get;set;}}
