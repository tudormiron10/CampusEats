using MediatR;

namespace CampusEats.Api.Features.Menu.Request;

public record ReorderMenuItemsRequest(List<Guid> OrderedMenuItemIds) : IRequest<IResult>;