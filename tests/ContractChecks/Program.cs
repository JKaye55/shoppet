using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using ShoppetAPI.Services;

var count=0;
void Check(bool value,string label){if(!value)throw new Exception("FAIL "+label);Console.WriteLine("PASS "+label);count++;}
Check(RbacService.NormalizeRole("PetOwner")=="Pet Owner","Legacy owner alias");
Check(RbacService.NormalizeRole("Super Admin")=="Admin","Legacy admin alias");
Check(RbacService.NormalizeRole("Clinic Owner")=="","Retired clinic role");
const string password="A careful pet owner passphrase";
var bcrypt=BCrypt.Net.BCrypt.HashPassword(password);
Check(CredentialMigration.Verify(password,bcrypt),"BCrypt login");
Check(!CredentialMigration.Verify("incorrect",bcrypt),"Wrong password rejected");
var identity=new PasswordHasher<object>().HashPassword(new object(),password);
Check(CredentialMigration.Verify(password,identity),"Existing web Identity login");
Check(!CredentialMigration.Verify(password,password),"Plaintext rejected");
Check(!CredentialMigration.Verify(password,"malformed"),"Malformed credential rejected");
var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"ConnectionStrings:SharedSqlServer","unused"}}).Build();
foreach(var pair in new[]{("Pets","GetPets"),("Profile","UpdateProfile"),("Cart","Checkout"),("HealthLogs","GetHealthLogs"),("Documents","Upload")})
{
 var http=new DefaultHttpContext();http.Request.Method="GET";
 var descriptor=new ControllerActionDescriptor{ControllerName=pair.Item1,ActionName=pair.Item2};
 var action=new ActionContext(http,new RouteData(),descriptor);
 var executing=new ActionExecutingContext(action,new List<IFilterMetadata>(),new Dictionary<string,object?>(),new object());bool called=false;
 await new SessionGuard(config).OnActionExecutionAsync(executing,()=>{called=true;return Task.FromResult(new ActionExecutedContext(action,new List<IFilterMetadata>(),new object()));});
 Check(!called&&executing.Result is UnauthorizedObjectResult,pair.Item1+" anonymous denied");
}
foreach(var pair in new[]{("Community","GetPosts"),("Marketplace","GetListings")})
{
 var http=new DefaultHttpContext();http.Request.Method="GET";var descriptor=new ControllerActionDescriptor{ControllerName=pair.Item1,ActionName=pair.Item2};
 var action=new ActionContext(http,new RouteData(),descriptor);var executing=new ActionExecutingContext(action,new List<IFilterMetadata>(),new Dictionary<string,object?>(),new object());bool called=false;
 await new SessionGuard(config).OnActionExecutionAsync(executing,()=>{called=true;return Task.FromResult(new ActionExecutedContext(action,new List<IFilterMetadata>(),new object()));});
 Check(called,pair.Item1+" public read");
}
Console.WriteLine($"{count} contract checks passed.");
