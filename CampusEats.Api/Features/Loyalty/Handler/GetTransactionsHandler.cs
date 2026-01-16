﻿using CampusEats.Api.Features.Loyalty.Request;
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

        var loyalty = await _context.Loyalties
            .FirstOrDefaultAsync(l => l.UserId == userId, cancellationToken);

        // Auto-create loyalty record if it doesn't exist
        if (loyalty == null)
        {
            loyalty = new Infrastructure.Persistence.Entities.Loyalty
            {
                LoyaltyId = Guid.NewGuid(),
                UserId = userId.Value,
                CurrentPoints = 0,
                LifetimePoints = 0
            };
            _context.Loyalties.Add(loyalty);
            await _context.SaveChangesAsync(cancellationToken);
            
            // New loyalty record has no transactions
            return Results.Ok(new List<LoyaltyTransactionResponse>());
        }

        var transactions = await _context.LoyaltyTransactions
            .AsNoTracking()
            .Where(t => t.LoyaltyId == loyalty.LoyaltyId)
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
