using Microsoft.EntityFrameworkCore;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

// Import ALL feature folders
using CampusEats.Api.Features.Categories.Request;
using CampusEats.Api.Features.Categories.Handler;
using CampusEats.Api.Features.Upload.Request;
using CampusEats.Api.Features.Kitchen.Request;
using CampusEats.Api.Features.Menu.Request;
using CampusEats.Api.Features.Order.Request;
using CampusEats.Api.Features.Orders.Requests;
using CampusEats.Api.Features.Payments.Request;
using CampusEats.Api.Features.Payments.Handler;
using CampusEats.Api.Features.User.Request;
using CampusEats.Api.Features.DietaryTags.Request;
using CampusEats.Api.Features.Loyalty.Request;
using CampusEats.Api.Features.Admin.Request;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Infrastructure.Extensions;
using CampusEats.Api.Hubs;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

// --- API Tag Constants ---
const string TagCategories = "Categories";
const string TagMenu = "Menu";
const string TagDietaryTags = "DietaryTags";
const string TagUpload = "Upload";
const string TagOrders = "Orders";
const string TagPayments = "Payments";
const string TagKitchen = "Kitchen";
const string TagUsers = "Users";
const string TagAdmin = "Admin";
const string TagLoyalty = "Loyalty";

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<CampusEats.Api.Infrastructure.Persistence.CampusEatsDbContext>(options =>
    options.UseNpgsql(connectionString));

// Configure JSON options to handle enums as strings
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // Add JWT Bearer auth to Swagger UI
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "CampusEats API", Version = "v1" });
    var securityScheme = new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter 'Bearer {token}'",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new Microsoft.OpenApi.Models.OpenApiReference
        {
            Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
            Id = "Bearer"
        }
    };
    c.AddSecurityDefinition("Bearer", securityScheme);
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            securityScheme,
            Array.Empty<string>()
        }
    });
});
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// Add HttpContextAccessor for accessing user claims in handlers
builder.Services.AddHttpContextAccessor();

// --- Register Stripe Webhook Handler ---
builder.Services.AddScoped<StripeWebhookHandler>();

// --- CORS Configuration for Blazor Frontend ---
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// --- Validator Registration ---
builder.Services.AddValidatorsFromAssemblyContaining<Program>(); // Scans and registers all validators in this assembly

// --- JWT Authentication Configuration ---
var jwtKey = builder.Configuration.GetSection("AppSettings:Token").Value
    ?? throw new InvalidOperationException("JWT Token key not configured in AppSettings:Token");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        // Handle JWT for SignalR WebSocket connections
        // WebSockets cannot use HTTP headers, so we extract the token from the query string
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                // Only extract from query string for SignalR hub paths
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/api/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// --- SignalR Configuration ---
builder.Services.AddSignalR();

var app = builder.Build();

// Enable Swagger in all environments for API documentation
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

// Enable CORS only when allowed origins are configured (not needed for same-origin deployment)
if (allowedOrigins.Length > 0)
{
    app.UseCors();
}

// Enable authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

// Enable static files for serving uploaded images and Blazor WASM
app.UseStaticFiles();
app.UseBlazorFrameworkFiles();

// --- DATABASE MIGRATION & SEEDING ---
// Run migrations automatically on startup (for production deployment)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CampusEatsDbContext>();
    await db.Database.MigrateAsync();
}


// --- Endpoint Mapping ---

// Health check endpoint for Azure monitoring
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
   .WithTags("Health");

// Create API route group
var api = app.MapGroup("/api");

// ====== MENU GROUP ======

// Endpoint for creating a new menu item (Staff: Manager/Admin).
api.MapPost("/menu", async (
        CreateMenuItemRequest request,
        HttpContext httpContext,
        [FromServices] IMediator mediator) =>
{
    if (!httpContext.IsStaff())
        return Results.Forbid();

    return await mediator.Send(request);
})
.RequireAuthorization()
.WithTags(TagMenu);

// Endpoint for retrieving the menu, with optional filtering (public).
api.MapGet("/menu", async (
        [AsParameters] GetMenuRequest request,
        [FromServices] IMediator mediator) =>
{
    return await mediator.Send(request);
})
.WithTags(TagMenu);

// Endpoint for retrieving a specific menu item by ID (public).
api.MapGet("/menu/{menuItemId:guid}", async (
        Guid menuItemId,
        [FromServices] IMediator mediator) =>
{
    return await mediator.Send(new GetMenuItemByIdRequest(menuItemId));
})
.WithTags(TagMenu);

// Endpoint for updating a specific menu item (Staff: Manager/Admin).
api.MapPut("/menu/{menuItemId:guid}", async (
        Guid menuItemId,
        UpdateMenuItemRequest request,
        HttpContext httpContext,
        [FromServices] IMediator mediator) =>
{
    if (!httpContext.IsStaff())
        return Results.Forbid();

    var requestWithId = request with { MenuItemId = menuItemId };
    return await mediator.Send(requestWithId);
})
.RequireAuthorization()
.WithTags(TagMenu);

// Endpoint for deleting a specific menu item (Staff: Manager/Admin).
api.MapDelete("/menu/{menuItemId:guid}", async (
        Guid menuItemId,
        HttpContext httpContext,
        [FromServices] IMediator mediator) =>
{
    if (!httpContext.IsStaff())
        return Results.Forbid();

    return await mediator.Send(new DeleteMenuItemRequest(menuItemId));
})
.RequireAuthorization()
.WithTags(TagMenu);

// Endpoint for reordering menu items (Staff: Manager/Admin).
api.MapPatch("/menu/reorder", async (
        ReorderMenuItemsRequest request,
        HttpContext httpContext,
        [FromServices] IMediator mediator) =>
{
    if (!httpContext.IsStaff())
        return Results.Forbid();

    return await mediator.Send(request);
})
.RequireAuthorization()
.WithTags(TagMenu);

// ====== CATEGORIES GROUP ======

// Endpoint for retrieving all categories (public).
api.MapGet("/categories", async ([FromServices] IMediator mediator) =>
    await mediator.Send(new GetCategoriesRequest())
)
.WithTags(TagCategories);

// Endpoint for creating a new category (Staff: Manager/Admin).
api.MapPost("/categories", async (
        CreateCategoryRequest request,
        HttpContext httpContext,
        [FromServices] IMediator mediator) =>
{
    if (!httpContext.IsStaff())
        return Results.Forbid();

    return await mediator.Send(request);
})
.RequireAuthorization()
.WithTags(TagCategories);

// Endpoint for updating a category (Staff: Manager/Admin).
api.MapPut("/categories/{categoryId:guid}", async (
        Guid categoryId,
        UpdateCategoryRequest request,
        HttpContext httpContext,
        [FromServices] IMediator mediator) =>
{
    if (!httpContext.IsStaff())
        return Results.Forbid();

    var requestWithId = request with { CategoryId = categoryId };
    return await mediator.Send(requestWithId);
})
.RequireAuthorization()
.WithTags(TagCategories);

// Endpoint for deleting a category (Staff: Manager/Admin).
api.MapDelete("/categories/{categoryId:guid}", async (
        Guid categoryId,
        HttpContext httpContext,
        [FromServices] IMediator mediator) =>
{
    if (!httpContext.IsStaff())
        return Results.Forbid();

    return await mediator.Send(new DeleteCategoryRequest(categoryId));
})
.RequireAuthorization()
.WithTags(TagCategories);

// Endpoint for reordering categories (Staff: Manager/Admin).
api.MapPatch("/categories/reorder", async (
        ReorderCategoriesRequest request,
        HttpContext httpContext,
        [FromServices] IMediator mediator) =>
{
    if (!httpContext.IsStaff())
        return Results.Forbid();

    return await mediator.Send(request);
})
.RequireAuthorization()
.WithTags(TagCategories);

// ====== DIETARY TAGS GROUP ======

// Endpoint for retrieving all dietary tags (public).
api.MapGet("/dietary-tags", async ([FromServices] IMediator mediator) =>
    await mediator.Send(new GetDietaryTagsRequest())
)
.WithTags(TagDietaryTags);

// Endpoint for creating a new dietary tag (Staff: Manager/Admin).
api.MapPost("/dietary-tags", async (
        CreateDietaryTagRequest request,
        HttpContext httpContext,
        [FromServices] IMediator mediator) =>
{
    if (!httpContext.IsStaff())
        return Results.Forbid();

    return await mediator.Send(request);
})
.RequireAuthorization()
.WithTags(TagDietaryTags);

// Endpoint for updating a dietary tag (Staff: Manager/Admin).
api.MapPut("/dietary-tags/{dietaryTagId:guid}", async (
        Guid dietaryTagId,
        UpdateDietaryTagRequest request,
        HttpContext httpContext,
        [FromServices] IMediator mediator) =>
{
    if (!httpContext.IsStaff())
        return Results.Forbid();

    var requestWithId = request with { DietaryTagId = dietaryTagId };
    return await mediator.Send(requestWithId);
})
.RequireAuthorization()
.WithTags(TagDietaryTags);

// Endpoint for deleting a dietary tag (Staff: Manager/Admin).
api.MapDelete("/dietary-tags/{dietaryTagId:guid}", async (
        Guid dietaryTagId,
        HttpContext httpContext,
        [FromServices] IMediator mediator) =>
{
    if (!httpContext.IsStaff())
        return Results.Forbid();

    return await mediator.Send(new DeleteDietaryTagRequest(dietaryTagId));
})
.RequireAuthorization()
.WithTags(TagDietaryTags);

// ====== UPLOAD GROUP ======

// Endpoint for uploading images (Staff: Manager/Admin).
api.MapPost("/upload/image", async (
        IFormFile file,
        HttpContext httpContext,
        [FromServices] IValidator<UploadImageRequest> validator,
        [FromServices] IMediator mediator,
        CancellationToken ct) =>
{
    if (!httpContext.IsStaff())
        return Results.Forbid();

    var request = new UploadImageRequest(file);

    var validationResult = await validator.ValidateAsync(request, ct);
    if (!validationResult.IsValid)
        return ApiErrors.ValidationFailed(validationResult.Errors[0].ErrorMessage);

    return await mediator.Send(request, ct);
})
.RequireAuthorization()
.WithTags(TagUpload)
.DisableAntiforgery();

// ====== ORDERS GROUP ======
// Endpoint for creating a new order (userId from JWT).
api.MapPost("/orders", async (
        CreateOrderRequest request,
        HttpContext httpContext,
        [FromServices] IMediator mediator) =>
{
    var userId = httpContext.GetUserId();
    if (userId == null)
        return ApiErrors.Unauthorized();

    // Override the userId from JWT (ignore any userId in request body)
    var secureRequest = request with { UserId = userId.Value };
    return await mediator.Send(secureRequest);
})
.RequireAuthorization()
.WithTags(TagOrders);

// Endpoint for canceling an order (with ownership check).
api.MapDelete("/orders/{orderId:guid}", async (
        Guid orderId,
        HttpContext httpContext,
        [FromServices] IMediator mediator,
        [FromServices] CampusEatsDbContext db) =>
{
    var userId = httpContext.GetUserId();
    if (userId == null)
        return ApiErrors.Unauthorized();

    // Check ownership (unless admin)
    var order = await db.Orders.FindAsync(orderId);
    if (order == null)
        return ApiErrors.OrderNotFound();

    if (order.UserId != userId && !httpContext.IsAdmin())
        return ApiErrors.Forbidden();

    return await mediator.Send(new CancelOrderRequest(orderId));
})
.RequireAuthorization()
.WithTags(TagOrders);

// Endpoint for retrieving a specific order by ID (with ownership check).
api.MapGet("/orders/{orderId:guid}", async (
        Guid orderId,
        HttpContext httpContext,
        [FromServices] IMediator mediator,
        [FromServices] CampusEatsDbContext db) =>
{
    var userId = httpContext.GetUserId();
    if (userId == null)
        return ApiErrors.Unauthorized();

    // Check ownership (unless staff)
    var order = await db.Orders.FindAsync(orderId);
    if (order == null)
        return ApiErrors.OrderNotFound();

    if (order.UserId != userId && !httpContext.IsStaff())
        return ApiErrors.Forbidden();

    return await mediator.Send(new GetOrderByIdRequest(orderId));
})
.RequireAuthorization()
.WithTags(TagOrders);

// Endpoint for retrieving orders (user sees their own, staff sees all).
api.MapGet("/orders", async (
        HttpContext httpContext,
        [FromServices] IMediator mediator) =>
{
    var userId = httpContext.GetUserId();
    if (userId == null)
        return ApiErrors.Unauthorized();

    // Admin/Manager see all orders
    if (httpContext.IsStaff())
        return await mediator.Send(new GetAllOrdersRequest());

    // Regular users see only their own orders
    return await mediator.Send(new GetAllOrdersByUserIdRequest(userId.Value));
})
.RequireAuthorization()
.WithTags(TagOrders);

// ====== PAYMENTS GROUP ======

// Endpoint for initiating a checkout session (new flow - creates Order after payment succeeds)
api.MapPost("/checkout", async (
        InitiateCheckoutRequest request,
        [FromServices] IMediator mediator) =>
{
    return await mediator.Send(request);
})
.RequireAuthorization()
.WithTags(TagPayments);

// Endpoint for initiating a payment for an order (legacy - kept for backward compatibility).
api.MapPost("/payments", async (
        CreatePaymentRequest request,
        [FromServices] IMediator mediator) =>
{
    return await mediator.Send(request);
})
.WithTags(TagPayments);

// Endpoint for retrieving the payment history for a specific user.
api.MapGet("/payments/user/{userId:guid}", async (
        Guid userId,
        [FromServices] IMediator mediator) =>
{
    return await mediator.Send(new GetPaymentByUserIdRequest(userId));
})
.WithTags(TagPayments);

// Endpoint for handling payment confirmations (webhook).
api.MapPost("/payments/confirmation", async (
        PaymentConfirmationRequest request,
        [FromServices] IMediator mediator) =>
    {
        return await mediator.Send(request);
    })
.WithTags(TagPayments);

// Endpoint for Stripe webhook events (NO authentication - Stripe calls this).
api.MapPost("/payments/webhook", async (
        HttpRequest request,
        [FromServices] StripeWebhookHandler webhookHandler) =>
{
    return await webhookHandler.HandleWebhook(request);
})
.WithTags(TagPayments)
.ExcludeFromDescription(); // Hide from Swagger (external webhook)

// ====== KITCHEN GROUP (Manager/Admin only) ======

// Endpoint for staff to view active orders.
api.MapGet("/kitchen/orders", async (
        HttpContext httpContext,
        [FromServices] IMediator mediator) =>
{
    if (!httpContext.IsStaff())
        return Results.Forbid();

    return await mediator.Send(new GetKitchenOrdersRequest());
})
.RequireAuthorization()
.WithTags(TagKitchen);

// Consolidated endpoint to update order status (resource-centric PATCH)
api.MapPatch("/kitchen/orders/{orderId:guid}", async (
        Guid orderId,
        HttpContext httpContext,
        [FromBody] UpdateOrderStatusRequest body,
        [FromServices] IMediator mediator) =>
{
    if (!httpContext.IsStaff())
        return Results.Forbid();

    var request = body with { OrderId = orderId };
    return await mediator.Send(request);
})
.RequireAuthorization()
.WithTags(TagKitchen);


// Endpoint to get kitchen analytics.
api.MapGet("/kitchen/analytics", async (
        DateTime startDate,
        DateTime endDate,
        string groupBy,
        HttpContext httpContext,
        [FromServices] IMediator mediator) =>
{
    if (!httpContext.IsStaff())
        return Results.Forbid();

    return await mediator.Send(new GetAnalyticsRequest(startDate, endDate, groupBy));
})
.RequireAuthorization()
.WithTags(TagKitchen);

// ====== USER GROUP ======

// Endpoint for creating a new user (registration).
// Anonymous users can only create Client accounts.
// Admin can create any role.
api.MapPost("/users", async (
        CreateUserRequest request,
        HttpContext httpContext,
        [FromServices] IMediator mediator) =>
{
    // If not authenticated or not admin, force Client role
    var isAdmin = httpContext.User.Identity?.IsAuthenticated == true && httpContext.IsAdmin();
    if (!isAdmin && request.Role != UserRole.Client)
    {
        var secureRequest = request with { Role = UserRole.Client };
        return await mediator.Send(secureRequest);
    }

    return await mediator.Send(request);
})
.WithTags(TagUsers);

// Endpoint for retrieving all users (Admin only).
api.MapGet("/users", async (
        HttpContext httpContext,
        [FromServices] IMediator mediator) =>
{
    if (!httpContext.IsAdmin())
        return Results.Forbid();

    return await mediator.Send(new GetAllUserRequest());
})
.RequireAuthorization()
.WithTags(TagUsers);

// Endpoint for retrieving a specific user by ID (own data or Admin).
api.MapGet("/users/{userId:guid}", async (
        Guid userId,
        HttpContext httpContext,
        [FromServices] IMediator mediator) =>
{
    if (!httpContext.CanAccessUserData(userId))
        return Results.Forbid();

    return await mediator.Send(new GetUserByIdRequest(userId));
})
.RequireAuthorization()
.WithTags(TagUsers);

// Endpoint for updating a user's information.
api.MapPut("/users/{userId:guid}", async (
        Guid userId,
        UpdateUserRequest request,
        HttpContext httpContext,
        [FromServices] IMediator mediator,
        [FromServices] CampusEatsDbContext db) =>
{
    var currentUserId = httpContext.GetUserId();
    if (currentUserId == null)
        return ApiErrors.Unauthorized();

    var isAdmin = httpContext.IsAdmin();
    var isOwnData = currentUserId == userId;

    // Must be admin or updating own data
    if (!isAdmin && !isOwnData)
        return ApiErrors.Forbidden();

    // If not admin, prevent role changes
    if (!isAdmin)
    {
        var existingUser = await db.Users.FindAsync(userId);
        if (existingUser == null)
            return ApiErrors.UserNotFound();

        // Force keeping existing role
        var secureRequest = request with { UserId = userId, Role = existingUser.Role };
        return await mediator.Send(secureRequest);
    }

    var requestWithId = request with { UserId = userId };
    return await mediator.Send(requestWithId);
})
.RequireAuthorization()
.WithTags(TagUsers);

// Endpoint for changing user password.
api.MapPatch("/users/{userId:guid}/password", async (
        Guid userId,
        ChangePasswordRequest request,
        HttpContext httpContext,
        [FromServices] IMediator mediator) =>
{
    var currentUserId = httpContext.GetUserId();
    if (currentUserId == null)
        return ApiErrors.Unauthorized();

    // Users can only change their own password
    if (currentUserId != userId)
        return ApiErrors.Forbidden();

    var requestWithId = request with { UserId = userId };
    return await mediator.Send(requestWithId);
})
.RequireAuthorization()
.WithTags(TagUsers);

// Endpoint for login (public)
api.MapPost("/users/login", async (
        LoginRequest request,
        [FromServices] IMediator mediator) =>
    {
        return await mediator.Send(request);
    })
    .WithTags(TagUsers);

// ====== ADMIN GROUP (Admin only) ======

// Endpoint for retrieving paginated users with search/filter (Admin only).
api.MapGet("/admin/users", async (
        int page,
        int pageSize,
        string? search,
        string? role,
        HttpContext httpContext,
        [FromServices] IMediator mediator) =>
{
    if (!httpContext.IsAdmin())
        return Results.Forbid();

    return await mediator.Send(new GetUsersWithPaginationRequest(page, pageSize, search, role));
})
.RequireAuthorization()
.WithTags(TagAdmin);

// Endpoint for deleting a user (Admin only).
api.MapDelete("/users/{userId:guid}", async (
        Guid userId,
        HttpContext httpContext,
        [FromServices] IMediator mediator) =>
{
    if (!httpContext.IsAdmin())
        return Results.Forbid();

    var currentUserId = httpContext.GetUserId();
    if (currentUserId == null)
        return ApiErrors.Unauthorized();

    return await mediator.Send(new DeleteUserRequest(userId, currentUserId.Value));
})
.RequireAuthorization()
.WithTags(TagAdmin);

// Endpoint for admin dashboard stats (Admin only).
api.MapGet("/admin/stats", async (
        HttpContext httpContext,
        [FromServices] IMediator mediator) =>
{
    if (!httpContext.IsAdmin())
        return Results.Forbid();

    return await mediator.Send(new GetAdminStatsRequest());
})
.RequireAuthorization()
.WithTags(TagAdmin);

// ============================================
// LOYALTY ENDPOINTS
// ============================================

// Client endpoints (require authentication)
api.MapGet("/loyalty", async (HttpContext httpContext, [FromServices] IMediator mediator) =>
    await mediator.Send(new GetLoyaltyStatusRequest(httpContext)))
    .RequireAuthorization()
    .WithTags(TagLoyalty);

api.MapGet("/loyalty/transactions", async (HttpContext httpContext, [FromServices] IMediator mediator) =>
    await mediator.Send(new GetTransactionsRequest(httpContext)))
    .RequireAuthorization()
    .WithTags(TagLoyalty);

api.MapGet("/loyalty/offers", async (HttpContext httpContext, [FromServices] IMediator mediator) =>
    await mediator.Send(new GetOffersRequest(httpContext)))
    .RequireAuthorization()
    .WithTags(TagLoyalty);

api.MapPost("/loyalty/offers/{offerId:guid}/redeem", async (Guid offerId, HttpContext httpContext, [FromServices] IMediator mediator) =>
    await mediator.Send(new RedeemOfferRequest(offerId, httpContext)))
    .RequireAuthorization()
    .WithTags(TagLoyalty);

// Manager endpoints (require Manager or Admin role)
api.MapGet("/loyalty/offers/manage", async (HttpContext httpContext, [FromServices] IMediator mediator) =>
    await mediator.Send(new GetAllOffersRequest(httpContext)))
    .RequireAuthorization()
    .WithTags(TagLoyalty);

api.MapPost("/loyalty/offers", async (CreateOfferRequestBody body, HttpContext httpContext, [FromServices] IMediator mediator) =>
    await mediator.Send(new CreateOfferRequest(
        body.Title,
        body.Description,
        body.ImageUrl,
        body.PointCost,
        body.MinimumTier,
        body.Items,
        httpContext
    )))
    .RequireAuthorization()
    .WithTags(TagLoyalty);

api.MapPut("/loyalty/offers/{offerId:guid}", async (Guid offerId, CreateOfferRequestBody body, HttpContext httpContext, [FromServices] IMediator mediator) =>
    await mediator.Send(new UpdateOfferRequest(
        offerId,
        body.Title,
        body.Description,
        body.ImageUrl,
        body.PointCost,
        body.MinimumTier,
        body.Items,
        httpContext
    )))
    .RequireAuthorization()
    .WithTags(TagLoyalty);

api.MapDelete("/loyalty/offers/{offerId:guid}", async (Guid offerId, HttpContext httpContext, [FromServices] IMediator mediator) =>
    await mediator.Send(new DeleteOfferRequest(offerId, httpContext)))
    .RequireAuthorization()
    .WithTags(TagLoyalty);

api.MapPatch("/loyalty/offers/{offerId:guid}/status", async (Guid offerId, UpdateOfferStatusBody body, HttpContext httpContext, [FromServices] IMediator mediator) =>
    await mediator.Send(new UpdateOfferStatusRequest(offerId, body.IsActive, httpContext)))
    .RequireAuthorization()
    .WithTags(TagLoyalty);

// ====== SIGNALR HUBS ======
api.MapHub<OrderHub>("/hubs/orders");

// Fallback to Blazor WASM SPA for client-side routing
app.MapFallbackToFile("index.html");

await app.RunAsync();

// Request body records for minimal API binding (Loyalty)
namespace CampusEats.Api.Features.Loyalty.Request
{
    public record CreateOfferRequestBody(
        string Title,
        string? Description,
        string? ImageUrl,
        int PointCost,
        string? MinimumTier,
        List<CreateOfferItemRequest> Items
    );

    public record UpdateOfferStatusBody(bool IsActive);
}
