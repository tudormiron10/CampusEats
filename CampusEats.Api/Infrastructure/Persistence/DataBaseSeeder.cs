// Infrastructure/Persistence/DataSeeder.cs
using Bogus;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CampusEats.Api.Infrastructure.Persistence;

public static class DataSeeder
{
    public static void SeedDatabase(IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CampusEatsDbContext>();

        context.Database.EnsureCreated();

        // Verificăm dacă baza de date este goală
        if (context.Users.Any())
        {
            return; // Baza de date a fost deja populată
        }
        
        // Folosim localizarea "ro" pentru date în română
        var faker = new Faker("ro");

        // --- 1. Utilizatori Statici ---
        var adminUser = new User
        {
            UserId = Guid.Parse("a0000000-0000-0000-0000-000000000001"), // ID static pentru testare
            Name = "Admin",
            Email = "admin@campuseats.com",
            Role = UserRole.Admin
        };
        
        // --- 2. Articole de Meniu (Bogus) ---
        var menuCategories = new[] { "Supe", "Salate", "Fel Principal", "Desert", "Băuturi" };
        var menuItemFaker = new Faker<MenuItem>("ro")
            .RuleFor(m => m.MenuItemId, f => Guid.NewGuid())
            .RuleFor(m => m.Name, f => f.Commerce.ProductName())
            .RuleFor(m => m.Price, f => f.Random.Decimal(10, 70))
            .RuleFor(m => m.Category, f => f.PickRandom(menuCategories))
            .RuleFor(m => m.Description, f => f.Lorem.Sentence(5))
            .RuleFor(m => m.ImageUrl, f => f.Image.PicsumUrl());

        var fakeMenuItems = menuItemFaker.Generate(20);

        // --- 3. Clienți (Bogus) ---
        var clientUserFaker = new Faker<User>("ro")
            .RuleFor(u => u.UserId, f => Guid.NewGuid())
            .RuleFor(u => u.Name, f => f.Name.FullName())
            .RuleFor(u => u.Email, (f, u) => f.Internet.Email(u.Name))
            .RuleFor(u => u.Role, UserRole.Client);
        
        var fakeClients = clientUserFaker.Generate(4);

        // --- 4. Conturi de Loialitate (Bogus) ---
        var fakeLoyalties = new List<Loyalty>();
        foreach (var client in fakeClients)
        {
            fakeLoyalties.Add(new Loyalty
            {
                LoyaltyId = Guid.NewGuid(),
                UserId = client.UserId,
                Points = faker.Random.Int(0, 500)
            });
        }
        
        // --- 5. Comenzi și Plăți (Bogus) ---
        var fakeOrders = new List<Order>();
        var fakePayments = new List<Payment>();

        // Stări posibile pentru comenzi
        var orderStatuses = new[] { OrderStatus.Completed, OrderStatus.Pending, OrderStatus.InPreparation, OrderStatus.Cancelled };

        foreach (var client in fakeClients)
        {
            var orderCount = faker.Random.Int(2, 3); 

            for (int i = 0; i < orderCount; i++)
            {
                var itemsForOrder = faker.PickRandom(fakeMenuItems, faker.Random.Int(1, 3)).ToList();
                var orderStatus = faker.PickRandom(orderStatuses);
                
                var newOrder = new Order
                {
                    OrderId = Guid.NewGuid(),
                    UserId = client.UserId,
                    Items = itemsForOrder,
                    TotalAmount = itemsForOrder.Sum(item => item.Price),
                    Status = orderStatus,
                    OrderDate = faker.Date.Past(1).ToUniversalTime() 
                };
                fakeOrders.Add(newOrder);

                // Dacă comanda este finalizată, creăm și o plată reușită
                if (newOrder.Status == OrderStatus.Completed)
                {
                    fakePayments.Add(new Payment
                    {
                        PaymentId = Guid.NewGuid(),
                        OrderId = newOrder.OrderId,
                        Amount = newOrder.TotalAmount,
                        Status = PaymentStatus.Successful
                    });
                }
            }
        }

        // --- Adăugarea în Baza de Date ---
        context.Users.Add(adminUser);
        context.Users.AddRange(fakeClients);
        context.Loyalties.AddRange(fakeLoyalties);
        context.MenuItems.AddRange(fakeMenuItems);
        context.Orders.AddRange(fakeOrders);
        context.Payments.AddRange(fakePayments);

        context.SaveChanges();
    }
}