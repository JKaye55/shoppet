using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;
using System.Security.Claims;
namespace ShoppetAPI.Services;

// A claimed user ID is never an authentication credential.
public sealed class SessionGuard(IConfiguration configuration) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var action=(ControllerActionDescriptor)context.ActionDescriptor;
        var controller=action.ControllerName;
        var publicRead = context.HttpContext.Request.Method=="GET" &&
            ((controller=="Marketplace" && action.ActionName=="GetListings") ||
             (controller=="Community" && action.ActionName is "GetPosts" or "GetComments") || controller=="PublicPetId");
        if(controller=="Auth" || publicRead) { await next();return; }
        var authorization=context.HttpContext.Request.Headers.Authorization.ToString();
        if(!authorization.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase) || authorization.Length<20)
        { context.Result=new UnauthorizedObjectResult("Sign in to continue.");return; }
        await using var c=new SqlConnection(configuration.GetConnectionString("SharedSqlServer"));
        await c.OpenAsync();
        int actor;string role;
        await using(var q=new SqlCommand("SELECT Id,Role FROM UserAccounts WHERE ApiToken=@Token AND ApiTokenExpiresAt>SYSUTCDATETIME()",c))
        {
            q.Parameters.AddWithValue("@Token",authorization[7..].Trim());
            await using var r=await q.ExecuteReaderAsync();
            if(!await r.ReadAsync()) {context.Result=new UnauthorizedObjectResult("Session expired. Sign in again.");return;}
            actor=r.GetInt32(0);role=RbacService.NormalizeRole(r.GetString(1));
        }
        if(string.IsNullOrEmpty(role)) {context.Result=new ObjectResult("Account role is inactive."){StatusCode=403};return;}
        foreach(var pair in context.ActionArguments)
        {
            if(pair.Key is "userId" or "currentUserId")
            {
                if(pair.Value is int value && value!=0 && value!=actor) {Deny(context);return;}
            }
            if(pair.Value is not null && !pair.Value.GetType().IsPrimitive && pair.Value is not string)
                foreach(var name in new[]{"UserId","SenderId"})
                {var prop=pair.Value.GetType().GetProperty(name);if(prop?.GetValue(pair.Value) is int id && id!=actor){Deny(context);return;}}
        }
        if(controller=="Pets" && action.ActionName=="GetPets") context.ActionArguments["userId"]=actor;
        if(context.ActionArguments.TryGetValue("petId",out var petValue) && petValue is int petId)
            if(!await OwnsPet(c,actor,petId)){Deny(context);return;}
        foreach(var value in context.ActionArguments.Values)
            if(value?.GetType().GetProperty("PetId")?.GetValue(value) is int pet && pet>0 && !await OwnsPet(c,actor,pet)) {Deny(context);return;}
        if(controller=="Pets" && action.ActionName is "UpdatePet" or "DeletePet"
            && context.ActionArguments["id"] is int petKey && !await OwnsPet(c,actor,petKey)){Deny(context);return;}
        if(role=="Admin" && context.HttpContext.Request.Method!="GET" &&
            !(context.HttpContext.Request.Method=="DELETE" && controller is "Marketplace" or "Community")) {Deny(context);return;}
        context.HttpContext.User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,actor.ToString()),new Claim(ClaimTypes.Role,role)},"SharedSession"));
        await next();
    }
    static void Deny(ActionExecutingContext c)=>c.Result=new ObjectResult("You do not have access to this record."){StatusCode=403};
    static async Task<bool> OwnsPet(SqlConnection c,int actor,int id)
    {await using var q=new SqlCommand("SELECT COUNT(1) FROM PetProfiles WHERE Id=@Id AND UserId=@User",c);q.Parameters.AddWithValue("@Id",id);q.Parameters.AddWithValue("@User",actor);return Convert.ToInt32(await q.ExecuteScalarAsync())==1;}
}
