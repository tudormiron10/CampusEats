using MediatR;

namespace CampusEats.Api.Features.Upload.Request;

public record UploadImageRequest(
    IFormFile File
) : IRequest<IResult>;
