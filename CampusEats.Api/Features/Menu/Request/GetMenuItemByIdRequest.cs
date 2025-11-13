using MediatR;
using System;

namespace CampusEats.Api.Features.Menu.Request;

public record GetMenuItemByIdRequest(Guid MenuItemId) : IRequest<IResult>;

