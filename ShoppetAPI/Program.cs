using ShoppetAPI.Services;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddScoped<SessionGuard>();
builder.Services.AddControllers(o => o.Filters.AddService<SessionGuard>());
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

try
{
    if(app.Configuration.GetValue("InitializeDatabase",true))
    {
        await SharedSchemaInitializer.EnsureAsync(app.Configuration);
        await CommunitySchemaInitializer.EnsureAsync(app.Configuration);
        await CredentialMigration.EnsureAsync(app.Configuration);
    }
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Shared database initialization failed. Startup stopped; see the SQL error above.");
    throw;
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Android emulator uses http://10.0.2.2:5020 during local development.
// Do not redirect that local HTTP request to the development HTTPS certificate.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

var wwwrootFolder = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
var uploadsFolder = Path.Combine(wwwrootFolder, "uploads");
Directory.CreateDirectory(Path.Combine(uploadsFolder, "community"));
Directory.CreateDirectory(Path.Combine(uploadsFolder, "documents"));

app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();

app.MapGet("/health/ready", async (IConfiguration config) =>
{
    try
    {
        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(config.GetConnectionString("SharedSqlServer"));
        await connection.OpenAsync();
        await using var command = new Microsoft.Data.SqlClient.SqlCommand("SELECT TOP(0) Id,ApiToken,ApiTokenExpiresAt,IsDisabled FROM dbo.UserAccounts; SELECT TOP(0) Reference,Subtotal,Total FROM dbo.MarketplaceOrders; SELECT TOP(0) Quantity,MarketplaceListingId,CartId FROM dbo.MarketplaceCartItems;", connection);
        await command.ExecuteNonQueryAsync();
        return Results.Ok(new { status = "ready" });
    }
    catch { return Results.StatusCode(503); }
});

app.Run();
