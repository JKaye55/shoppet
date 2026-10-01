namespace ShoppetAPI.Services;
public static class MediaUrls
{
    public static string Canonical(string? value)=>(value??"").Replace("http://10.0.2.2:","http://localhost:",StringComparison.OrdinalIgnoreCase);
    public static string Web(string? value,IConfiguration config)=>string.Join(",",Canonical(value).Split(',',StringSplitOptions.RemoveEmptyEntries).Select(u=>u.StartsWith("/")?(config["PublicWebBaseUrl"]??"http://localhost:5253").TrimEnd('/')+u:u));
}
