using CampusEats.Api.Features.Upload.Request;
using CampusEats.Api.Features.Upload.Response;
using MediatR;

namespace CampusEats.Api.Features.Upload.Handler;

public class UploadImageHandler : IRequestHandler<UploadImageRequest, IResult>
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<UploadImageHandler> _logger;

    public UploadImageHandler(IWebHostEnvironment environment, ILogger<UploadImageHandler> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async Task<IResult> Handle(UploadImageRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var file = request.File;

            // Generate unique filename with original extension
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid()}{extension}";
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "images", "menu");

            // Create directory if it doesn't exist
            Directory.CreateDirectory(uploadsFolder);

            var filePath = Path.Combine(uploadsFolder, fileName);

            // Save file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            // Return relative path
            var relativePath = $"/images/menu/{fileName}";
            var response = new UploadImageResponse(relativePath);

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading image");
            return Results.Problem("Error uploading image");
        }
    }
}
