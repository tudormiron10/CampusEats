using MediatR;

namespace CampusEats.Api.Features.Categories.Request;

public record CreateCategoryRequest(
    string Name,
    string Icon,
    int SortOrder
) : IRequest<IResult>;
