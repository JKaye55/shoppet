using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;
namespace ShoppetAPI.Services;

public static class SharedSchemaInitializer
{
    public static async Task EnsureAsync(IConfiguration config)
    {
        var file = Path.Combine(AppContext.BaseDirectory, "Database", "SharedIntegration.sql");
        await using var connection = new SqlConnection(config.GetConnectionString("SharedSqlServer"));
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        // Both clients may start together. Serialize shared schema changes.
        await using (var schemaLock = new SqlCommand("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='ShoppetCare.SharedSchema', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=120000; IF @r<0 THROW 51000,'Could not acquire schema migration lock.',1;", connection, transaction))
        {
            schemaLock.CommandTimeout = 130;
            await schemaLock.ExecuteNonQueryAsync();
        }
        var batchNumber = 0;
        foreach (var batch in Regex.Split(await File.ReadAllTextAsync(file), @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(batch)) continue;
            batchNumber++;
            await using var command = new SqlCommand(batch, connection, transaction) { CommandTimeout = 120 };
            try { await command.ExecuteNonQueryAsync(); }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"SharedIntegration.sql batch {batchNumber} failed: {ex.Message}", ex);
            }
        }
        await transaction.CommitAsync();
    }
}
