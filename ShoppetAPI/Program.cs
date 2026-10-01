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
    app.Logger.LogError(ex, "Community SQL Server schema initialization failed.");
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

app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();

app.Run();
