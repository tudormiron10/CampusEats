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
using CampusEats.Api.Features.User.Request;
using CampusEats.Api.Features.DietaryTags.Request;
using CampusEats.Api.Infrastructure.Persistence;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using CampusEats.Api.Infrastructure.Extensions;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

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
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Enable CORS
app.UseCors();

// Enable authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

// Enable static files for serving uploaded images
app.UseStaticFiles();

// --- SEEDER CODE BLOCK ---
// Run the seeder only in the Development environment
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    try
    {
        // Call our static seeding method
        DataSeeder.SeedDatabase(app);
    }
    catch (Exception ex)
    {
        // Log the error if seeding fails
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred during database seeding.");
    }
}

// --- Endpoint Mapping ---

// ====== MENU GROUP ======

// Endpoint for creating a new menu item (Staff: Manager/Admin).
app.MapPost("/menu", async (
        CreateMenuItemRequest request,
        HttpContext httpContext,
        [FromServices] IMediator mediator) =>
{
    if (!httpContext.IsStaff())
        return Results.Forbid();

    return await mediator.Send(request);
})
.RequireAuthorization()
.WithTags("Menu");

// Endpoint for retrieving the menu, with optional filtering (public).
app.MapGet("/menu", async (
        [AsParameters] GetMenuRequest request,
        [FromServices] IMediator mediator) =>
{
    return await mediator.Send(request);
})
.WithTags("Menu");

// Endpoint for retrieving a specific menu item by ID (public).
app.MapGet("/menu/{menuItemId:guid}", async (
        Guid menuItemId,
        [FromServices] IMediator mediator) =>
{
    return await mediator.Send(new GetMenuItemByIdRequest(menuItemId));
})
.WithTags("Menu");

// Endpoint for updating a specific menu item (Staff: Manager/Admin).
app.MapPut("/menu/{menuItemId:guid}", async (
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
.WithTags("Menu");

// Endpoint for deleting a specific menu item (Staff: Manager/Admin).
app.MapDelete("/menu/{menuItemId:guid}", async (
        Guid menuItemId,
        HttpContext httpContext,
        [FromServices] IMediator mediator) =>
{
    if (!httpContext.IsStaff())
        return Results.Forbid();

    return await mediator.Send(new DeleteMenuItemRequest(menuItemId));
})
.RequireAuthorization()
.WithTags("Menu");

// Endpoint for reordering menu items (Staff: Manager/Admin).
app.MapPatch("/menu/reorder", async (
        ReorderMenuItemsRequest request,
        HttpContext httpContext,
        [FromServices] IMediator mediator) =>
{
    if (!httpContext.IsStaff())
        return Results.Forbid();

    return await mediator.Send(request);
})
.RequireAuthorization()
.WithTags("Menu");

// ====== CATEGORIES GROUP ======

// Endpoint for retrieving all categories (public).
app.MapGet("/categories", async ([FromServices] IMediator mediator) =>
    await mediator.Send(new GetCategoriesRequest())
)
.WithTags("Categories");

// Endpoint for creating a new category (Staff: Manager/Admin).
app.MapPost("/categories", async (
        CreateCategoryRequest request,
        HttpContext httpContext,
        [FromServices] IMediator mediator) =>
{
    if (!httpContext.IsStaff())
        return Results.Forbid();

    return await mediator.Send(request);
})
.RequireAuthorization()
.WithTags("Categories");

// Endpoint for updating a category (Staff: Manager/Admin).
app.MapPut("/categories/{categoryId:guid}", async (
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
.WithTags("Categories");

// Endpoint for deleting a category (Staff: Manager/Admin).
app.MapDelete("/categories/{categoryId:guid}", async (
        Guid categoryId,
        HttpContext httpContext,
        [FromServices] IMediator mediator) =>
{
    if (!httpContext.IsStaff())
        return Results.Forbid();

    return await mediator.Send(new DeleteCategoryRequest(categoryId));
})
.RequireAuthorization()
.WithTags("Categories");

// Endpoint for reordering categories (Staff: Manager/Admin).
app.MapPatch("/categories/reorder", async (
        ReorderCategoriesRequest request,
        HttpContext httpContext,
        [FromServices] IMediator mediator) =>
{
    if (!httpContext.IsStaff())
        return Results.Forbid();

    return await mediator.Send(request);
})
.RequireAuthorization()
.WithTags("Categories");

// ====== DIETARY TAGS GROUP ======

// Endpoint for retrieving all dietary tags (public).
app.MapGet("/dietary-tags", async ([FromServices] IMediator mediator) =>
    await mediator.Send(new GetDietaryTagsRequest())
)
.WithTags("DietaryTags");

// Endpoint for creating a new dietary tag (Staff: Manager/Admin).
app.MapPost("/dietary-tags", async (
        CreateDietaryTagRequest request,
        HttpContext httpContext,
        [FromServices] IMediator mediator) =>
{
    if (!httpContext.IsStaff())
        return Results.Forbid();

    return await mediator.Send(request);
})
.RequireAuthorization()
.WithTags("DietaryTags");

// Endpoint for updating a dietary tag (Staff: Manager/Admin).
app.MapPut("/dietary-tags/{dietaryTagId:guid}", async (
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
.WithTags("DietaryTags");

// Endpoint for deleting a dietary tag (Staff: Manager/Admin).
app.MapDelete("/dietary-tags/{dietaryTagId:guid}", async (
        Guid dietaryTagId,
        HttpContext httpContext,
        [FromServices] IMediator mediator) =>
{
    if (!httpContext.IsStaff())
        return Results.Forbid();

    return await mediator.Send(new DeleteDietaryTagRequest(dietaryTagId));
})
.RequireAuthorization()
.WithTags("DietaryTags");

// ====== UPLOAD GROUP ======

// Endpoint for uploading images (Staff: Manager/Admin).
app.MapPost("/upload/image", async (
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
        return ApiErrors.ValidationFailed(validationResult.Errors.First().ErrorMessage);

    return await mediator.Send(request, ct);
})
.RequireAuthorization()
.WithTags("Upload")
.DisableAntiforgery();

// ====== ORDERS GROUP ======
// Endpoint for creating a new order (userId from JWT).
app.MapPost("/orders", async (
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
.WithTags("Orders");

// Endpoint for canceling an order (with ownership check).
app.MapDelete("/orders/{orderId:guid}", async (
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
.WithTags("Orders");

// Endpoint for retrieving a specific order by ID (with ownership check).
app.MapGet("/orders/{orderId:guid}", async (
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
.WithTags("Orders");

// Endpoint for retrieving orders (user sees their own, staff sees all).
app.MapGet("/orders", async (
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
.WithTags("Orders");

// ====== PAYMENTS GROUP ======

// Endpoint for initiating a payment for an order.
app.MapPost("/payments", async (
        CreatePaymentRequest request,
        [FromServices] IMediator mediator) =>
{
    return await mediator.Send(request);
})
.WithTags("Payments");

// Endpoint for retrieving the payment history for a specific user.
app.MapGet("/payments/user/{userId:guid}", async (
        Guid userId,
        [FromServices] IMediator mediator) =>
{
    return await mediator.Send(new GetPaymentByUserIdRequest(userId));
})
.WithTags("Payments");

// Endpoint for handling payment confirmations (webhook).
app.MapPost("/payments/confirmation", async (
        PaymentConfirmationRequest request,
        [FromServices] IMediator mediator) =>
    {
        return await mediator.Send(request);
    })
.WithTags("Payments");

// ====== KITCHEN GROUP (Manager/Admin only) ======

// Endpoint for staff to view active orders.
app.MapGet("/kitchen/orders", async (
        HttpContext httpContext,
        [FromServices] IMediator mediator) =>
{
    if (!httpContext.IsStaff())
        return Results.Forbid();

    return await mediator.Send(new GetKitchenOrdersRequest());
})
.RequireAuthorization()
.WithTags("Kitchen");

// Consolidated endpoint to update order status (resource-centric PATCH)
app.MapPatch("/kitchen/orders/{orderId:guid}", async (
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
.WithTags("Kitchen");


// Endpoint to get kitchen analytics.
app.MapGet("/kitchen/analytics", async (
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
.WithTags("Kitchen");

// ====== USER GROUP ======

// Endpoint for creating a new user (registration).
// Anonymous users can only create Client accounts.
// Admin can create any role.
app.MapPost("/users", async (
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
.WithTags("Users");

// Endpoint for retrieving all users (Admin only).
app.MapGet("/users", async (
        HttpContext httpContext,
        [FromServices] IMediator mediator) =>
{
    if (!httpContext.IsAdmin())
        return Results.Forbid();

    return await mediator.Send(new GetAllUserRequest());
})
.RequireAuthorization()
.WithTags("Users");

// Endpoint for retrieving a specific user by ID (own data or Admin).
app.MapGet("/users/{userId:guid}", async (
        Guid userId,
        HttpContext httpContext,
        [FromServices] IMediator mediator) =>
{
    if (!httpContext.CanAccessUserData(userId))
        return Results.Forbid();

    return await mediator.Send(new GetUserByIdRequest(userId));
})
.RequireAuthorization()
.WithTags("Users");

// Endpoint for updating a user's information.
// Users can update their own data but cannot change their role.
// Admin can update any user and change roles.
app.MapPut("/users/{userId:guid}", async (
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
.WithTags("Users");

// Endpoint for login (public)
app.MapPost("/users/login", async (
        LoginRequest request,
        [FromServices] IMediator mediator) =>
    {
        return await mediator.Send(request);
    })
    .WithTags("Users");

app.Run();