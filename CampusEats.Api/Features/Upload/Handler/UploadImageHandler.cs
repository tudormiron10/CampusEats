using CampusEats.Api.Features.Upload.Request;
using CampusEats.Api.Features.Upload.Response;
using MediatR;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace CampusEats.Api.Features.Upload.Handler
{
    public class UploadImageHandler : IRequestHandler<UploadImageRequest, IResult>
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<UploadImageHandler> _logger;

        public UploadImageHandler(IWebHostEnvironment env, ILogger<UploadImageHandler> logger)
        {
            _env = env ?? throw new ArgumentNullException(nameof(env));
            _logger = logger;
        }

        public async Task<IResult> Handle(UploadImageRequest request, CancellationToken cancellationToken)
        {
            try
            {
                // Validate file
                if (request.File == null || request.File.Length == 0)
                {
                    _logger.LogWarning("Upload requested with no file.");
                    return Results.BadRequest("No file provided.");
                }

                // Resolve web root reliably (use WebRootPath if available, fallback to ContentRootPath/wwwroot or current directory)
                var webRoot = !string.IsNullOrWhiteSpace(_env.WebRootPath)
                    ? _env.WebRootPath
                    : Path.Combine(_env.ContentRootPath ?? Directory.GetCurrentDirectory(), "wwwroot");

                // Build uploads folder under actual web root and ensure it exists
                var uploadsFolder = Path.Combine(webRoot, "images", "menuitems");
                Directory.CreateDirectory(uploadsFolder);

                // Create unique filename and save
                var extension = Path.GetExtension(request.File.FileName);
                var fileName = $"{Guid.NewGuid()}{extension}";
                var fullPath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await request.File.CopyToAsync(stream, cancellationToken);
                }

                // Compute a URL path relative to web root, normalize to forward slashes and ensure leading slash
                var relativePath = Path.GetRelativePath(webRoot, fullPath).Replace('\\', '/');
                if (!relativePath.StartsWith("/")) relativePath = "/" + relativePath;

                // Log physical and URL paths for debugging preview 404s
                _logger.LogInformation("Saved image to physical path: {FullPath}", fullPath);
                _logger.LogInformation("Image URL path: {UrlPath}", relativePath);

                return Results.Ok(new { path = relativePath });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading image");
                return Results.Problem("Error uploading image");
            }
        }
    }
}
