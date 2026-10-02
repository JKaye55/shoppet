using Microsoft.AspNetCore.Mvc;
using QRCoder;
namespace ShoppetAPI.Controllers;
[ApiController,Route("api/publicpetid")]
public class PublicPetIdController(IConfiguration config):ControllerBase
{
 [HttpGet("{cardId}/qr")]
 public IActionResult Qr(string cardId)
 {
  if(cardId.Length>100||cardId.Any(c=>!char.IsLetterOrDigit(c)&&c!='-'))return BadRequest();
  var link=(config["PublicWebBaseUrl"]??"http://localhost:5253").TrimEnd('/')+"/pet/card/"+Uri.EscapeDataString(cardId);
  using var generator=new QRCodeGenerator();using var data=generator.CreateQrCode(link,QRCodeGenerator.ECCLevel.Q);using var png=new PngByteQRCode(data);return File(png.GetGraphic(8),"image/png");
 }
 [HttpGet("configuration")]
 public object Configuration()=>new{PublicWebBaseUrl=config["PublicWebBaseUrl"]??"http://localhost:5253"};
}
