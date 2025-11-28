using CampusEats.Api.Features.DietaryTags.Request;
using CampusEats.Api.Features.DietaryTags.Response;
using CampusEats.Api.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Features.DietaryTags.Handler;

public class GetDietaryTagsHandler : IRequestHandler<GetDietaryTagsRequest, IResult>
{
    private readonly CampusEatsDbContext _context;

    public GetDietaryTagsHandler(CampusEatsDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(GetDietaryTagsRequest request, CancellationToken cancellationToken)
    {
        var dietaryTags = await _context.DietaryTags
            .AsNoTracking()
            .OrderBy(dt => dt.Name)
            .Select(dt => new DietaryTagResponse(dt.DietaryTagId, dt.Name))
            .ToListAsync(cancellationToken);

        return Results.Ok(dietaryTags);
    }
}