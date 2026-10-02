using Microsoft.Data.SqlClient;

namespace ShoppetAPI.Services;

public static class RbacService
{
    public const string PetOwnerRole = "Pet Owner";
    public const string AdminRole = "Admin";

    public static string NormalizeRole(string? role)
    {
        var value = (role ?? string.Empty).Trim();

        if (value.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Super Admin", StringComparison.OrdinalIgnoreCase))
            return AdminRole;

        if (value.Equals("Pet Owner", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("PetOwner", StringComparison.OrdinalIgnoreCase))
            return PetOwnerRole;

        return string.Empty;
    }

    public static bool IsPetOwner(string? role) =>
        NormalizeRole(role).Equals(PetOwnerRole, StringComparison.OrdinalIgnoreCase);

    public static bool IsAdmin(string? role) =>
        NormalizeRole(role).Equals(AdminRole, StringComparison.OrdinalIgnoreCase);

    public static async Task<string?> GetActiveRoleAsync(
        SqlConnection connection,
        int userId,
        SqlTransaction? transaction = null)
    {
        if (userId <= 0) return null;

        await using var cmd = new SqlCommand(
            "SELECT Role FROM UserAccounts WHERE Id=@UserId",
            connection,
            transaction);

        cmd.Parameters.AddWithValue("@UserId", userId);
        var value = await cmd.ExecuteScalarAsync();

        if (value is null || value == DBNull.Value)
            return null;

        var role = NormalizeRole(Convert.ToString(value));
        return string.IsNullOrWhiteSpace(role) ? null : role;
    }

    public static async Task<bool> IsPetOwnerAsync(
        SqlConnection connection,
        int userId,
        SqlTransaction? transaction = null)
    {
        var role = await GetActiveRoleAsync(connection, userId, transaction);
        return role is not null && IsPetOwner(role);
    }

    public static async Task<bool> IsAdminAsync(
        SqlConnection connection,
        int userId,
        SqlTransaction? transaction = null)
    {
        var role = await GetActiveRoleAsync(connection, userId, transaction);
        return role is not null && IsAdmin(role);
    }
}
