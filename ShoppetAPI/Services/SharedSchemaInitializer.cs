using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;
namespace ShoppetAPI.Services;
public static class SharedSchemaInitializer
{
 public static async Task EnsureAsync(IConfiguration config)
 {
    var file=Path.Combine(AppContext.BaseDirectory,"Database","SharedIntegration.sql");
    await using var c=new SqlConnection(config.GetConnectionString("SharedSqlServer"));await c.OpenAsync();
    foreach(var batch in Regex.Split(await File.ReadAllTextAsync(file),@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase))
    {if(string.IsNullOrWhiteSpace(batch))continue;await using var q=new SqlCommand(batch,c){CommandTimeout=120};await q.ExecuteNonQueryAsync();}
 }
}
