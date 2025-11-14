using MediatR;

namespace CampusEats.Api.Features.Categories.Request;

public record UpdateCategoryRequest(
    Guid CategoryId,
    string Name,
    string Icon,
    int SortOrder
) : IRequest<IResult>;
