using MediatR;
using System;

namespace CampusEats.Api.Features.Menu.Request;

public record DeleteMenuItemRequest(Guid MenuItemId) : IRequest<IResult>;

