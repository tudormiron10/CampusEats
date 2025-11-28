using CampusEats.Api.Features.DietaryTags.Request;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Extensions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Features.DietaryTags.Handler;

public class DeleteDietaryTagHandler : IRequestHandler<DeleteDietaryTagRequest, IResult>
{
    private readonly CampusEatsDbContext _context;

    public DeleteDietaryTagHandler(CampusEatsDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(DeleteDietaryTagRequest request, CancellationToken cancellationToken)
    {
        var dietaryTag = await _context.DietaryTags.FindAsync(request.DietaryTagId);
        if (dietaryTag == null)
            return ApiErrors.NotFound("Dietary tag");

        // Check if dietary tag is in use by any menu items
        var isInUse = await _context.MenuItemDietaryTags
            .AnyAsync(mdt => mdt.DietaryTagId == request.DietaryTagId, cancellationToken);

        if (isInUse)
            return ApiErrors.Conflict("Cannot delete dietary tag as it is in use by menu items.");

        _context.DietaryTags.Remove(dietaryTag);
        await _context.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}