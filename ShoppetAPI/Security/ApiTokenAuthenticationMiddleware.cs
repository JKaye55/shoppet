using Microsoft.Data.SqlClient;
using System.Security.Claims;

namespace ShoppetAPI.Security;

public sealed class ApiTokenAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;

    public ApiTokenAuthenticationMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;

        if (!path.StartsWithSegments("/api") ||
            path.StartsWithSegments("/api/auth/login") ||
            path.StartsWithSegments("/api/auth/register"))
        {
            await _next(context);
            return;
        }

        var authorization = context.Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Authentication required.");
            return;
        }

        var token = authorization["Bearer ".Length..].Trim();
        if (token.Length is < 32 or > 128)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Invalid authentication token.");
            return;
        }

        var connectionString =
            _configuration.GetConnectionString("SharedSqlServer")
            ?? throw new InvalidOperationException("SharedSqlServer connection string not found.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var cmd = new SqlCommand(@"
            SELECT Id,Role
            FROM UserAccounts
            WHERE ApiToken=@Token
              AND ApiTokenExpiresAt IS NOT NULL
              AND ApiTokenExpiresAt>SYSUTCDATETIME();", connection);
        cmd.Parameters.AddWithValue("@Token", token);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Session expired. Please sign in again.");
            return;
        }

        int userId = reader.GetInt32(0);
        string role = reader.IsDBNull(1) ? "Pet Owner" : reader.GetString(1);

        context.User = new ClaimsPrincipal(
            new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role)
            ], "ShoppetApiToken"));

        await _next(context);
    }
}

public static class ControllerUserExtensions
{
    public static int AuthenticatedUserId(this Microsoft.AspNetCore.Mvc.ControllerBase controller)
    {
        var raw = controller.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var userId) ? userId : 0;
    }

    public static bool IsAuthenticatedUser(
        this Microsoft.AspNetCore.Mvc.ControllerBase controller,
        int userId) =>
        userId > 0 && controller.AuthenticatedUserId() == userId;
}
