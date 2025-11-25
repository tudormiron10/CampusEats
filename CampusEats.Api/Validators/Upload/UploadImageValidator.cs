using CampusEats.Api.Features.Upload.Request;
using FluentValidation;

namespace CampusEats.Api.Validators.Upload;

public class UploadImageValidator : AbstractValidator<UploadImageRequest>
{
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    private const long MaxFileSize = 5 * 1024 * 1024; // 5MB

    public UploadImageValidator()
    {
        RuleFor(x => x.File)
            .NotNull()
            .WithMessage("No file uploaded");
        When(x => x.File != null, () =>
        {
            RuleFor(x => x.File.Length)
                .GreaterThan(0)
                .WithMessage("File is empty")
                .LessThanOrEqualTo(MaxFileSize)
                .WithMessage("File size must not exceed 5MB");

            RuleFor(x => x.File.FileName)
                .Must(HasValidExtension)
                .WithMessage("Invalid file type. Only images (jpg, jpeg, png, gif, webp) are allowed");
        });
    }

    private static bool HasValidExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return AllowedExtensions.Contains(extension);
    }
}
