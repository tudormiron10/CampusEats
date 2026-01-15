using CampusEats.Api.Features.DietaryTags.Request;
using CampusEats.Api.Features.DietaryTags.Response;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Extensions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Features.DietaryTags.Handler;

public class UpdateDietaryTagHandler : IRequestHandler<UpdateDietaryTagRequest, IResult>
{
    private readonly CampusEatsDbContext _context;

    public UpdateDietaryTagHandler(CampusEatsDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(UpdateDietaryTagRequest request, CancellationToken cancellationToken)
    {
        var dietaryTag = await _context.DietaryTags.FindAsync(new object[] { request.DietaryTagId }, cancellationToken);
        if (dietaryTag == null)
            return ApiErrors.NotFound("Dietary tag");

        // Check if another dietary tag with the same name already exists
        var exists = await _context.DietaryTags
            .AnyAsync(dt => dt.Name.ToLower() == request.Name.ToLower()
                         && dt.DietaryTagId != request.DietaryTagId, cancellationToken);

        if (exists)
            return ApiErrors.Conflict("A dietary tag with this name already exists.");

        dietaryTag.Name = request.Name.Trim();
        await _context.SaveChangesAsync(cancellationToken);

        var response = new DietaryTagResponse(dietaryTag.DietaryTagId, dietaryTag.Name);
        return Results.Ok(response);
    }
}