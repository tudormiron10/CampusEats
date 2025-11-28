using MediatR;

namespace CampusEats.Api.Features.DietaryTags.Request;

public record CreateDietaryTagRequest(
    string Name
) : IRequest<IResult>;