using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
namespace ShoppetAPI.Services;
public static class CredentialMigration
{
    public static bool Verify(string password, string hash)
    {
        try { return hash.StartsWith("$2", StringComparison.Ordinal)
            ? BCrypt.Net.BCrypt.Verify(password, hash)
            : new PasswordHasher<object>().VerifyHashedPassword(new object(), hash, password) != PasswordVerificationResult.Failed; }
        catch { return false; }
    }
    public static async Task EnsureAsync(IConfiguration config)
    {
        await using var c = new SqlConnection(config.GetConnectionString("SharedSqlServer"));
        await c.OpenAsync();
        var rows = new List<(int Id,string Hash)>();
        await using (var q = new SqlCommand("SELECT Id,PasswordHash FROM UserAccounts",c))
        await using (var r = await q.ExecuteReaderAsync())
            while(await r.ReadAsync()) if(!r.IsDBNull(1)) rows.Add((r.GetInt32(0),r.GetString(1)));
        foreach(var row in rows)
        {
            if(row.Hash.StartsWith("$2",StringComparison.Ordinal) || string.IsNullOrEmpty(row.Hash)) continue;
            try { var b=Convert.FromBase64String(row.Hash); if(b.Length>=49 && (b[0]==0 || b[0]==1)) continue; } catch(FormatException) { }
            await using var q=new SqlCommand("UPDATE UserAccounts SET PasswordHash=@Hash WHERE Id=@Id AND PasswordHash=@Old",c);
            q.Parameters.AddWithValue("@Hash",BCrypt.Net.BCrypt.HashPassword(row.Hash));
            q.Parameters.AddWithValue("@Id",row.Id); q.Parameters.AddWithValue("@Old",row.Hash);
            await q.ExecuteNonQueryAsync();
        }

        const string adminEmail = "julliannekayefernando@gmail.com";
        const string adminPass = "akonalangsana";
        var hasher = new PasswordHasher<object>();
        var hash = hasher.HashPassword(new object(), adminPass);

        await using (var checkCmd = new SqlCommand("SELECT Id FROM UserAccounts WHERE LOWER(Email) = @Email", c))
        {
            checkCmd.Parameters.AddWithValue("@Email", adminEmail.ToLowerInvariant());
            var existingId = await checkCmd.ExecuteScalarAsync();
            if (existingId is not null && existingId != DBNull.Value)
            {
                await using var updateCmd = new SqlCommand("UPDATE UserAccounts SET PasswordHash = @Hash, Role = 'Admin', IsDisabled = 0, FullName = 'Jullianne Kaye Fernando' WHERE Id = @Id", c);
                updateCmd.Parameters.AddWithValue("@Hash", hash);
                updateCmd.Parameters.AddWithValue("@Id", Convert.ToInt32(existingId));
                await updateCmd.ExecuteNonQueryAsync();
            }
            else
            {
                await using var insertCmd = new SqlCommand(@"
                    INSERT INTO UserAccounts (FullName, Email, PasswordHash, Role, IsDisabled, CreatedAt, ShowSocialLinksOnMarketplace)
                    VALUES ('Jullianne Kaye Fernando', @Email, @Hash, 'Admin', 0, SYSDATETIME(), 1)", c);
                insertCmd.Parameters.AddWithValue("@Email", adminEmail);
                insertCmd.Parameters.AddWithValue("@Hash", hash);
                await insertCmd.ExecuteNonQueryAsync();
            }
        }
    }
}
