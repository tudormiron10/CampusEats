using CampusEats.Api.Features.Loyalty.Request;
using CampusEats.Api.Infrastructure.Extensions;
using CampusEats.Api.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Features.Loyalty.Handler;

public class DeleteOfferHandler : IRequestHandler<DeleteOfferRequest, IResult>
{
    private readonly CampusEatsDbContext _context;

    public DeleteOfferHandler(CampusEatsDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(DeleteOfferRequest request, CancellationToken cancellationToken)
    {
        if (!request.HttpContext.IsStaff())
            return Results.Forbid();

        var offer = await _context.Offers
            .FirstOrDefaultAsync(o => o.OfferId == request.OfferId, cancellationToken);

        if (offer == null)
            return ApiErrors.NotFound("Offer");

        _context.Offers.Remove(offer);
        await _context.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}
