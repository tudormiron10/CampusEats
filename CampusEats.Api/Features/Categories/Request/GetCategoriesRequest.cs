using MediatR;

namespace CampusEats.Api.Features.Categories.Request;

public record GetCategoriesRequest() : IRequest<IResult>;
