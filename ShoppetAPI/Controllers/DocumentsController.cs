using Microsoft.AspNetCore.Mvc;
namespace ShoppetAPI.Controllers;
[ApiController,Route("api/documents")]
public class DocumentsController(IWebHostEnvironment environment,IConfiguration config):ControllerBase
{
 [HttpPost,RequestSizeLimit(55*1024*1024)]
 public async Task<IActionResult> Upload([FromForm]List<IFormFile> files)
 {
  if(files.Count is <1 or >5)return BadRequest("Choose one to five care documents.");
  foreach(var f in files)if(f.Length is <=0 or >10*1024*1024||!new[]{".pdf",".jpg",".jpeg",".png",".webp"}.Contains(Path.GetExtension(f.FileName).ToLowerInvariant()))return BadRequest("Choose PDF or image documents up to 10 MB each.");
  var folder=Path.Combine(environment.WebRootPath??Path.Combine(environment.ContentRootPath,"wwwroot"),"uploads","documents");Directory.CreateDirectory(folder);var urls=new List<string>();
  foreach(var f in files){var name=Guid.NewGuid().ToString("N")+Path.GetExtension(f.FileName).ToLowerInvariant();await using var stream=System.IO.File.Create(Path.Combine(folder,name));await f.CopyToAsync(stream);urls.Add((config["PublicApiBaseUrl"]??"http://localhost:5020").TrimEnd('/')+"/uploads/documents/"+name);}
  return Ok(urls);
 }
}
