using MediatR;

namespace CampusEats.Api.Features.DietaryTags.Request;

public record UpdateDietaryTagRequest(
    Guid DietaryTagId,
    string Name
) : IRequest<IResult>;