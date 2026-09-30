using ShoppetAPI.Security;
using ShoppetAPI.Services;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddSingleton<SharedDatabaseInitializer>();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Ensure the shared SQL Server schema contains every table/column required by
// the current Web + MAUI integration. The initializer is idempotent.
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<SharedDatabaseInitializer>();
    await initializer.InitializeAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Local Android emulators connect to the host through http://10.0.2.2.
// Do not redirect development HTTP traffic to the local HTTPS certificate,
// because the emulator does not trust the ASP.NET development certificate.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseMiddleware<ApiTokenAuthenticationMiddleware>();

app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();

app.Run();
