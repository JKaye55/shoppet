using MySql.Data.MySqlClient;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var configuredConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(configuredConnectionString))
{
    var envConnectionString = Environment.GetEnvironmentVariable("SHOPPET_DB_CONNECTION");
    if (!string.IsNullOrWhiteSpace(envConnectionString))
    {
        builder.Configuration["ConnectionStrings:DefaultConnection"] = envConnectionString;
    }
}

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("ShoppetClients", policy =>
    {
        if (allowedOrigins.Length == 0 || allowedOrigins.Contains("*"))
        {
            policy
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
            return;
        }

        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("ShoppetClients");

app.UseAuthorization();

app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));

app.MapGet("/health/ready", async (IConfiguration configuration, CancellationToken cancellationToken) =>
{
    var connectionString = configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return Results.Problem(
            detail: "Database connection is not configured. Set ConnectionStrings__DefaultConnection or SHOPPET_DB_CONNECTION.",
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Not Ready");
    }

    try
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand("SELECT 1;", connection);
        await command.ExecuteScalarAsync(cancellationToken);
        return Results.Ok(new { status = "Ready" });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: $"Database probe failed: {ex.Message}",
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Not Ready");
    }
});

app.MapControllers();

app.Run();

public partial class Program { }
