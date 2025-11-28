using CampusEats.Api.Features.DietaryTags.Request;
using CampusEats.Api.Features.DietaryTags.Response;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Infrastructure.Extensions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Features.DietaryTags.Handler;

public class CreateDietaryTagHandler : IRequestHandler<CreateDietaryTagRequest, IResult>
{
    private readonly CampusEatsDbContext _context;

    public CreateDietaryTagHandler(CampusEatsDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(CreateDietaryTagRequest request, CancellationToken cancellationToken)
    {
        // Check if a dietary tag with the same name already exists
        var exists = await _context.DietaryTags
            .AnyAsync(dt => dt.Name.ToLower() == request.Name.ToLower(), cancellationToken);

        if (exists)
            return ApiErrors.Conflict("A dietary tag with this name already exists.");

        var dietaryTag = new DietaryTag
        {
            DietaryTagId = Guid.NewGuid(),
            Name = request.Name.Trim()
        };

        _context.DietaryTags.Add(dietaryTag);
        await _context.SaveChangesAsync(cancellationToken);

        var response = new DietaryTagResponse(dietaryTag.DietaryTagId, dietaryTag.Name);
        return Results.Created($"/dietary-tags/{dietaryTag.DietaryTagId}", response);
    }
}