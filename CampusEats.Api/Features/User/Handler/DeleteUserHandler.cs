using MediatR;
using Microsoft.EntityFrameworkCore;
using CampusEats.Api.Features.User.Request;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Infrastructure.Extensions;

namespace CampusEats.Api.Features.User.Handler;

public class DeleteUserHandler : IRequestHandler<DeleteUserRequest, IResult>
{
    private readonly CampusEatsDbContext _context;

    public DeleteUserHandler(CampusEatsDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(DeleteUserRequest request, CancellationToken cancellationToken)
    {
        // Prevent self-deletion
        if (request.UserId == request.CurrentUserId)
        {
            return ApiErrors.CannotDeleteSelf();
        }

        // Find the user with their loyalty and orders
        var user = await _context.Users
            .Include(u => u.Loyalty)
            .Include(u => u.Orders)
            .FirstOrDefaultAsync(u => u.UserId == request.UserId, cancellationToken);

        if (user == null)
        {
            return ApiErrors.UserNotFound();
        }

        // Check if user has active orders
        var activeStatuses = new[] { OrderStatus.Pending, OrderStatus.InPreparation, OrderStatus.Ready };
        var hasActiveOrders = user.Orders.Any(o => activeStatuses.Contains(o.Status));

        if (hasActiveOrders)
        {
            return ApiErrors.UserHasActiveOrders();
        }

        // If deleting an admin, check if they're the last one
        if (user.Role == UserRole.Admin)
        {
            var adminCount = await _context.Users
                .CountAsync(u => u.Role == UserRole.Admin, cancellationToken);

            if (adminCount <= 1)
            {
                return ApiErrors.CannotDeleteLastAdmin();
            }
        }

        // Set UserId to null for all user's orders (they will be preserved)
        foreach (var order in user.Orders)
        {
            order.UserId = null;
        }

        // Delete loyalty record if exists
        if (user.Loyalty != null)
        {
            // Also delete loyalty transactions
            var transactions = await _context.LoyaltyTransactions
                .Where(t => t.LoyaltyId == user.Loyalty.LoyaltyId)
                .ToListAsync(cancellationToken);

            _context.LoyaltyTransactions.RemoveRange(transactions);
            _context.Loyalties.Remove(user.Loyalty);
        }

        // Delete the user
        _context.Users.Remove(user);

        await _context.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}