using Microsoft.AspNetCore.Mvc;

namespace ShoppetAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MediaController : ControllerBase
{
    private const long MaxImageBytes = 5 * 1024 * 1024;
    private static readonly string[] AllowedAreas = ["pets", "community", "marketplace"];
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    private readonly IWebHostEnvironment _environment;

    public MediaController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    [HttpPost("image")]
    [RequestSizeLimit(MaxImageBytes + 1024 * 1024)]
    public async Task<IActionResult> UploadImage([FromForm] IFormFile? file, [FromForm] string? area)
    {
        if (file is null || file.Length == 0)
            return BadRequest("Choose an image to upload.");

        var cleanArea = (area ?? string.Empty).Trim().ToLowerInvariant();
        if (!AllowedAreas.Contains(cleanArea))
            return BadRequest("Upload area is not valid.");

        if (file.Length > MaxImageBytes)
            return BadRequest("Images must be 5 MB or smaller.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return BadRequest("Only JPG, PNG, and WebP images are supported.");

        var uploadsRoot = Path.Combine(
            _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"),
            "uploads",
            cleanArea);

        Directory.CreateDirectory(uploadsRoot);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(uploadsRoot, fileName);

        await using (var output = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(output);
        }

        // Store a server-relative path in SQL Server. Each client resolves this
        // to its own API host (Android emulator, Windows, or future hosting).
        var relativePath = $"/uploads/{cleanArea}/{fileName}";
        return Ok(new { path = relativePath });
    }
}
