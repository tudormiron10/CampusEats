using MediatR;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using System.Text.Json.Serialization;

namespace CampusEats.Api.Features.Kitchen.Request;

public record UpdateOrderStatusRequest([property: JsonIgnore] Guid OrderId, OrderStatus NewStatus) : IRequest<IResult>;
