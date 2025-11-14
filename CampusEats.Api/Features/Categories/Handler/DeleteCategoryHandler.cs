using CampusEats.Api.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Features.Categories.Handler;

public class DeleteCategoryHandler : IRequestHandler<DeleteCategoryRequest, IResult>
{
    private readonly CampusEatsDbContext _context;

    public DeleteCategoryHandler(CampusEatsDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(DeleteCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.CategoryId == request.CategoryId, cancellationToken);

        if (category == null)
        {
            return Results.NotFound(new { message = "Category not found" });
        }

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}

public record DeleteCategoryRequest(Guid CategoryId) : IRequest<IResult>;
