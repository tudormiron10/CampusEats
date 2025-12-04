using CampusEats.Api.Features.Loyalty.Request;
using CampusEats.Api.Features.Loyalty.Response;
using CampusEats.Api.Infrastructure.Extensions;
using CampusEats.Api.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Features.Loyalty.Handler;

public class GetTransactionsHandler : IRequestHandler<GetTransactionsRequest, IResult>
{
    private readonly CampusEatsDbContext _context;

    public GetTransactionsHandler(CampusEatsDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(GetTransactionsRequest request, CancellationToken cancellationToken)
    {
        var userId = request.HttpContext.GetUserId();
        if (userId == null)
            return Results.Unauthorized();

        var loyaltyId = await _context.Loyalties
            .Where(l => l.UserId == userId)
            .Select(l => l.LoyaltyId)
            .FirstOrDefaultAsync(cancellationToken);

        if (loyaltyId == Guid.Empty)
            return ApiErrors.NotFound("Loyalty record");

        var transactions = await _context.LoyaltyTransactions
            .AsNoTracking()
            .Where(t => t.LoyaltyId == loyaltyId)
            .OrderByDescending(t => t.Date)
            .Select(t => new LoyaltyTransactionResponse(
                t.TransactionId,
                t.Date,
                t.Description,
                t.Type,
                t.Points,
                t.OrderId
            ))
            .ToListAsync(cancellationToken);

        return Results.Ok(transactions);
    }
}
