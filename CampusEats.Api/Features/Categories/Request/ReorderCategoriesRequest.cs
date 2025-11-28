using MediatR;

namespace CampusEats.Api.Features.Categories.Request;

public record ReorderCategoriesRequest(List<Guid> OrderedCategoryIds) : IRequest<IResult>;