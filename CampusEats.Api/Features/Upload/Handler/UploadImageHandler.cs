using MediatR;

namespace CampusEats.Api.Features.Upload.Handler;

public class UploadImageHandler
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<UploadImageHandler> _logger;

    public UploadImageHandler(IWebHostEnvironment environment, ILogger<UploadImageHandler> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async Task<IResult> Handle(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return Results.BadRequest(new { message = "No file uploaded" });
        }

        // Validate file type
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
        {
            return Results.BadRequest(new { message = "Invalid file type. Only images are allowed." });
        }

        // Validate file size (max 5MB)
        if (file.Length > 5 * 1024 * 1024)
        {
            return Results.BadRequest(new { message = "File size must not exceed 5MB" });
        }

        try
        {
            // Generate unique filename
            var fileName = $"{Guid.NewGuid()}{extension}";
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "images", "menu");

            // Create directory if it doesn't exist
            Directory.CreateDirectory(uploadsFolder);

            var filePath = Path.Combine(uploadsFolder, fileName);

            // Save file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Return relative path
            var relativePath = $"/images/menu/{fileName}";
            return Results.Ok(new { path = relativePath });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading image");
            return Results.Problem("Error uploading image");
        }
    }
}
