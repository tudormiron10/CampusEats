using Microsoft.EntityFrameworkCore;
using MediatR;
using Microsoft.AspNetCore.Mvc;

// Import ALL feature folders
using CampusEats.Api.Features.Menu;
using CampusEats.Api.Features.Payments;
using CampusEats.Api.Features.Kitchen.Request;
using CampusEats.Api.Features.Order.Request;
using CampusEats.Api.Features.Orders.Request;
using CampusEats.Api.Features.Users;
using CampusEats.Api.Infrastructure.Persistence;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<CampusEats.Api.Infrastructure.Persistence.CampusEatsDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
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

// --- Handler Registration ---

// Menu
builder.Services.AddScoped<CreateMenuItemHandler>();
builder.Services.AddScoped<GetMenuHandler>();
builder.Services.AddScoped<GetMenuItemByIdHandler>();
builder.Services.AddScoped<UpdateMenuItemHandler>();
builder.Services.AddScoped<DeleteMenuItemHandler>();

// Payments
builder.Services.AddScoped<CreatePaymentHandler>();
builder.Services.AddScoped<GetPaymentByUserIdHandler>();
builder.Services.AddScoped<PaymentConfirmationHandler>();

// User
builder.Services.AddScoped<CreateUserHandler>();
builder.Services.AddScoped<GetAllUsersHandler>();
builder.Services.AddScoped<GetUserByIdHandler>();
builder.Services.AddScoped<UpdateUserHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Enable CORS
app.UseCors();

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
// Endpoint for creating a new menu item.
app.MapPost("/menu", async (
        CreateMenuItemRequest request,
        CreateMenuItemHandler handler,
        IValidator<CreateMenuItemRequest> validator) => // Inject the validator
    {
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary()); // Return validation errors
        }
        return await handler.Handle(request);
    })
    .WithTags("Menu");

// Endpoint for retrieving the menu, with optional filtering.
app.MapGet("/menu", async (
        [AsParameters] GetMenuRequest request, // [AsParameters] binds parameters from the query
        GetMenuHandler handler) =>
    {
        return await handler.Handle(request);
    })
    .WithTags("Menu");

// Endpoint for retrieving a specific menu item by ID.
app.MapGet("/menu/{menuItemId:guid}", async (
        Guid menuItemId,
        GetMenuItemByIdHandler handler) =>
    {
        return await handler.Handle(menuItemId);
    })
    .WithTags("Menu");

// Endpoint for updating a specific menu item.
app.MapPut("/menu/{menuItemId:guid}", async (
        Guid menuItemId,
        UpdateMenuItemRequest request,
        UpdateMenuItemHandler handler,
        IValidator<UpdateMenuItemRequest> validator) => // Inject the validator
    {
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary()); // Return validation errors
        }
        return await handler.Handle(menuItemId, request);
    })
    .WithTags("Menu");

// Endpoint for deleting a specific menu item.
app.MapDelete("/menu/{menuItemId:guid}", async (
        Guid menuItemId,
        DeleteMenuItemHandler handler) =>
    {
        return await handler.Handle(menuItemId);
    })
    .WithTags("Menu");

// ====== ORDERS GROUP ======
// Endpoint for creating a new order.
app.MapPost("/orders", async (
        CreateOrderRequest request,
        [FromServices] IMediator mediator) =>
    await mediator.Send(request)
)
.WithTags("Orders");

// Endpoint for canceling an order.
app.MapDelete("/orders/{orderId:guid}", async (
        Guid orderId,
        [FromServices] IMediator mediator) =>
    await mediator.Send(new CancelOrderRequest(orderId))
)
.WithTags("Orders");

// Endpoint for retrieving a specific order by ID.
app.MapGet("/orders/{orderId:guid}", async (
        Guid orderId,
        [FromServices] IMediator mediator) =>
    await mediator.Send(new GetOrderByIdRequest(orderId))
)
.WithTags("Orders");

// Endpoint for retrieving all orders.
app.MapGet("/orders", async ([FromServices] IMediator mediator) =>
    await mediator.Send(new GetAllOrdersRequest())
)
.WithTags("Orders");

// ====== PAYMENTS GROUP ======
// Endpoint for initiating a payment for an order.
app.MapPost("/payments", async (
        CreatePaymentRequest request,
        CreatePaymentHandler handler,
        IValidator<CreatePaymentRequest> validator) => // Inject the validator
    {
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary()); // Return validation errors
        }
        return await handler.Handle(request);
    })
    .WithTags("Payments");

// Endpoint for retrieving the payment history for a specific user.
app.MapGet("/payments/user/{userId:guid}", async (
        Guid userId,
        GetPaymentByUserIdHandler handler) => // Corrected from GetPaymentByUserIdHandler
    {
        return await handler.Handle(userId);
    })
    .WithTags("Payments");

// Endpoint for handling payment confirmations (webhook).
app.MapPost("/payments/confirmation", async (
        PaymentConfirmationRequest request,
        [FromServices] IMediator mediator) =>
    await mediator.Send(request)
)
.WithTags("Payments");

// ====== KITCHEN GROUP ======
// Endpoint for kitchen staff to view pending orders.
app.MapGet("/kitchen/orders", async ([FromServices] IMediator mediator) =>
    await mediator.Send(new GetKitchenOrdersRequest())
)
.WithTags("Kitchen");

// Endpoint to mark an order as being in preparation.
app.MapPost("/kitchen/orders/{orderId:guid}/prepare", async (
        Guid orderId,
        [FromServices] IMediator mediator) =>
    await mediator.Send(new PrepareOrderRequest(orderId))
)
.WithTags("Kitchen");

// Endpoint to mark an order as ready for pickup.
app.MapPost("/kitchen/orders/{orderId:guid}/ready", async (
        Guid orderId,
        [FromServices] IMediator mediator) =>
    await mediator.Send(new ReadyOrderRequest(orderId))
)
.WithTags("Kitchen");

// Endpoint to mark an order as completed.
app.MapPost("/kitchen/orders/{orderId:guid}/complete", async (
        Guid orderId,
        [FromServices] IMediator mediator) =>
    await mediator.Send(new CompleteOrderRequest(orderId))
)
.WithTags("Kitchen");

// Endpoint to get the daily sales report.
app.MapGet("/kitchen/report", async (
        DateOnly? date, // optional query parameter: ?date=2025-11-11
        [FromServices] IMediator mediator) =>
    await mediator.Send(new GetDailySalesReportRequest(date ?? DateOnly.FromDateTime(DateTime.UtcNow)))
)
.WithTags("Kitchen");

// ====== USER GROUP ======

// Endpoint for creating a new user.
app.MapPost("/users", async (
        CreateUserRequest request,
        CreateUserHandler handler,
        IValidator<CreateUserRequest> validator) => // Inject the validator
    {
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary()); // Return validation errors
        }
        return await handler.Handle(request);
    })
    .WithTags("Users");

// Endpoint for retrieving all users.
app.MapGet("/users", async (GetAllUsersHandler handler) =>
    {
        return await handler.Handle();
    })
    .WithTags("Users");

// Endpoint for retrieving a specific user by ID.
app.MapGet("/users/{userId:guid}", async (
        Guid userId,
        GetUserByIdHandler handler) =>
    {
        return await handler.Handle(userId);
    })
    .WithTags("Users");

// Endpoint for updating a user's information.
app.MapPut("/users/{userId:guid}", async (
        Guid userId,
        UpdateUserRequest request,
        UpdateUserHandler handler,
        IValidator<UpdateUserRequest> validator) => // Inject the validator
    {
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary()); // Return validation errors
        }
        return await handler.Handle(userId, request);
    })
    .WithTags("Users");

app.Run();