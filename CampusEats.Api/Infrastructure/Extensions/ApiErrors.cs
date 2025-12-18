namespace CampusEats.Api.Infrastructure.Extensions;

/// <summary>
/// Standardized API error response format.
/// All error responses follow this structure for consistency.
/// </summary>
public record ApiError(string Code, string Message);

/// <summary>
/// Helper class for creating standardized error responses.
/// </summary>
public static class ApiErrors
{
    // 400 Bad Request
    public static IResult BadRequest(string message) =>
        Results.BadRequest(new ApiError("BAD_REQUEST", message));

    public static IResult ValidationFailed(string message) =>
        Results.BadRequest(new ApiError("VALIDATION_FAILED", message));

    public static IResult InvalidOperation(string message) =>
        Results.BadRequest(new ApiError("INVALID_OPERATION", message));

    // 401 Unauthorized
    public static IResult Unauthorized(string message = "Authentication required.") =>
        Results.Json(new ApiError("UNAUTHORIZED", message), statusCode: 401);

    public static IResult InvalidCredentials() =>
        Results.Json(new ApiError("INVALID_CREDENTIALS", "Invalid email or password."), statusCode: 401);

    public static IResult IncorrectPassword() =>
        Results.Json(new ApiError("INCORRECT_PASSWORD", "Current password is incorrect."), statusCode: 401);

    // 403 Forbidden
    public static IResult Forbidden(string message = "You do not have permission to perform this action.") =>
        Results.Json(new ApiError("FORBIDDEN", message), statusCode: 403);

    // 409 Conflict
    public static IResult Conflict(string message) =>
        Results.Conflict(new ApiError("CONFLICT", message));

    public static IResult EmailAlreadyExists() =>
        Conflict("An account with this email already exists.");

    // 404 Not Found
    public static IResult NotFound(string resource) =>
        Results.NotFound(new ApiError("NOT_FOUND", $"{resource} not found."));

    public static IResult OrderNotFound() => NotFound("Order");
    public static IResult UserNotFound() => NotFound("User");
    public static IResult MenuItemNotFound() => NotFound("Menu item");
    public static IResult CategoryNotFound() => NotFound("Category");
    public static IResult PaymentNotFound() => NotFound("Payment");

    // Admin-specific errors
    public static IResult UserHasActiveOrders() =>
        Conflict("Cannot delete user with active orders (Pending, InPreparation, or Ready).");

    public static IResult CannotDeleteLastAdmin() =>
        Conflict("Cannot delete the last admin user.");

    public static IResult CannotDeleteSelf() =>
        Conflict("Cannot delete your own account.");
}