using MediatR;

namespace CampusEats.Api.Features.DietaryTags.Request;

public record GetDietaryTagsRequest() : IRequest<IResult>;