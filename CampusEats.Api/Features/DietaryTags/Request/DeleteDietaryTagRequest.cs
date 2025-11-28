using MediatR;

namespace CampusEats.Api.Features.DietaryTags.Request;

public record DeleteDietaryTagRequest(Guid DietaryTagId) : IRequest<IResult>;