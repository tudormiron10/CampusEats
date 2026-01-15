using Bogus;
using CampusEats.Api.Infrastructure.Persistence.Entities;
using System.Security.Cryptography; 

namespace CampusEats.Api.Infrastructure.Persistence;

public static class DataSeeder
{
    public static void SeedDatabase(IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CampusEatsDbContext>();

        // Verificăm dacă baza de date este goală
        if (context.Users.Any())
        {
            return; // Baza de date a fost deja populată
        }
        
        // Folosim localizarea "ro" pentru date în română
        var faker = new Faker("ro");

        // --- PREGĂTIREA PAROLEI IMPLICITE ---
        CreatePasswordHash("password123", out byte[] defaultHash, out byte[] defaultSalt);

        // --- 1. Utilizatori Statici ---
        var adminUser = CreateAdminUser(defaultHash, defaultSalt);
        
        // --- 2. Articole de Meniu (Bogus) ---
        var fakeMenuItems = GenerateMenuItems();

        // --- 3. Clienți (Bogus) ---
        var fakeClients = GenerateClients(faker, defaultHash, defaultSalt);

        // --- 4. Conturi de Loialitate (Bogus) ---
        var fakeLoyalties = GenerateLoyalties(faker, fakeClients);
        
        // --- 5. Comenzi și Plăți (Bogus) ---
        var (fakeOrders, fakePayments) = GenerateOrdersAndPayments(faker, fakeClients, fakeMenuItems, context);

        // --- 6. Categorii (Sample Categories) ---
        var categories = CreateCategories();

        // --- 7. Dietary Tags ---
        var dietaryTags = CreateDietaryTags();

        // --- Adăugarea în Baza de Date ---
        context.DietaryTags.AddRange(dietaryTags);
        context.Categories.AddRange(categories);
        context.Users.Add(adminUser);
        context.Users.AddRange(fakeClients);
        context.Loyalties.AddRange(fakeLoyalties);
        context.MenuItems.AddRange(fakeMenuItems);
        context.Orders.AddRange(fakeOrders);
        context.Payments.AddRange(fakePayments);

        // --- 8. Assign random dietary tags to menu items ---
        var menuItemDietaryTags = AssignDietaryTagsToMenuItems(faker, fakeMenuItems, dietaryTags);
        context.MenuItemDietaryTags.AddRange(menuItemDietaryTags);

        context.SaveChanges();
    }

    private static User CreateAdminUser(byte[] defaultHash, byte[] defaultSalt)
    {
        return new User
        {
            UserId = Guid.Parse("a0000000-0000-0000-0000-000000000001"),
            Name = "Admin",
            Email = "admin@campuseats.com",
            Role = UserRole.Admin,
            PasswordHash = defaultHash,
            PasswordSalt = defaultSalt
        };
    }

    private static List<MenuItem> GenerateMenuItems()
    {
        var menuCategories = new[] { "Supe", "Salate", "Fel Principal", "Desert", "Băuturi" };

        var menuItemFaker = new Faker<MenuItem>("ro")
            .RuleFor(m => m.MenuItemId, f => Guid.NewGuid())
            .RuleFor(m => m.Name, f => f.Commerce.ProductName())
            .RuleFor(m => m.Price, f => f.Random.Decimal(10, 70))
            .RuleFor(m => m.Category, f => f.PickRandom(menuCategories))
            .RuleFor(m => m.Description, f => f.Lorem.Sentence(5))
            .RuleFor(m => m.ImagePath, f => f.Image.PicsumUrl())
            .RuleFor(m => m.IsAvailable, f => true)
            .RuleFor(m => m.SortOrder, f => f.Random.Int(0, 100));

        return menuItemFaker.Generate(20);
    }

    private static List<User> GenerateClients(Faker faker, byte[] defaultHash, byte[] defaultSalt)
    {
        var clientUserFaker = new Faker<User>("ro")
            .RuleFor(u => u.UserId, f => Guid.NewGuid())
            .RuleFor(u => u.Name, f => f.Name.FullName())
            .RuleFor(u => u.Email, (f, u) => f.Internet.Email(u.Name))
            .RuleFor(u => u.Role, UserRole.Client)
            .RuleFor(u => u.PasswordHash, f => defaultHash)
            .RuleFor(u => u.PasswordSalt, f => defaultSalt);

        return clientUserFaker.Generate(4);
    }

    private static List<Loyalty> GenerateLoyalties(Faker faker, List<User> clients)
    {
        var loyalties = new List<Loyalty>();
        foreach (var client in clients)
        {
            loyalties.Add(new Loyalty
            {
                LoyaltyId = Guid.NewGuid(),
                UserId = client.UserId,
                CurrentPoints = faker.Random.Int(0, 500),
                LifetimePoints = faker.Random.Int(0, 500)
            });
        }
        return loyalties;
    }

    private static (List<Order> Orders, List<Payment> Payments) GenerateOrdersAndPayments(
        Faker faker,
        List<User> clients,
        List<MenuItem> menuItems,
        CampusEatsDbContext context)
    {
        var orders = new List<Order>();
        var payments = new List<Payment>();
        var orderStatuses = new[] { OrderStatus.Completed, OrderStatus.Pending, OrderStatus.InPreparation, OrderStatus.Cancelled };

        foreach (var client in clients)
        {
            var orderCount = faker.Random.Int(2, 3);

            for (int i = 0; i < orderCount; i++)
            {
                var itemsForOrder = faker.PickRandom(menuItems, faker.Random.Int(1, 3)).ToList();
                var orderStatus = faker.PickRandom(orderStatuses);

                var newOrder = new Order
                {
                    OrderId = Guid.NewGuid(),
                    UserId = client.UserId,
                    OrderDate = faker.Date.Past(1).ToUniversalTime(),
                    Status = orderStatus,
                    TotalAmount = 0
                };
                orders.Add(newOrder);

                var orderItems = CreateOrderItems(faker, newOrder.OrderId, itemsForOrder);
                context.OrderItems.AddRange(orderItems);

                newOrder.TotalAmount = orderItems.Sum(oi => oi.Quantity * oi.UnitPrice);

                if (newOrder.Status == OrderStatus.Completed)
                {
                    payments.Add(new Payment
                    {
                        PaymentId = Guid.NewGuid(),
                        OrderId = newOrder.OrderId,
                        Amount = newOrder.TotalAmount,
                        Status = PaymentStatus.Succeeded,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        return (orders, payments);
    }

    private static List<OrderItem> CreateOrderItems(Faker faker, Guid orderId, List<MenuItem> menuItems)
    {
        var orderItems = new List<OrderItem>();
        foreach (var menuItem in menuItems)
        {
            orderItems.Add(new OrderItem
            {
                OrderId = orderId,
                MenuItemId = menuItem.MenuItemId,
                Quantity = faker.Random.Int(1, 3),
                UnitPrice = menuItem.Price
            });
        }
        return orderItems;
    }

    private static List<Category> CreateCategories()
    {
        return new List<Category>
        {
            new Category { CategoryId = Guid.NewGuid(), Name = "Supe", Icon = "soup", SortOrder = 1 },
            new Category { CategoryId = Guid.NewGuid(), Name = "Salate", Icon = "salad", SortOrder = 2 },
            new Category { CategoryId = Guid.NewGuid(), Name = "Fel Principal", Icon = "utensils", SortOrder = 3 },
            new Category { CategoryId = Guid.NewGuid(), Name = "Desert", Icon = "cake", SortOrder = 4 },
            new Category { CategoryId = Guid.NewGuid(), Name = "Băuturi", Icon = "cup-soda", SortOrder = 5 }
        };
    }

    private static List<DietaryTag> CreateDietaryTags()
    {
        return new List<DietaryTag>
        {
            new DietaryTag { DietaryTagId = Guid.NewGuid(), Name = "Vegetarian" },
            new DietaryTag { DietaryTagId = Guid.NewGuid(), Name = "Vegan" },
            new DietaryTag { DietaryTagId = Guid.NewGuid(), Name = "Gluten-Free" },
            new DietaryTag { DietaryTagId = Guid.NewGuid(), Name = "Dairy-Free" },
            new DietaryTag { DietaryTagId = Guid.NewGuid(), Name = "Nut-Free" },
            new DietaryTag { DietaryTagId = Guid.NewGuid(), Name = "Halal" },
            new DietaryTag { DietaryTagId = Guid.NewGuid(), Name = "Kosher" },
            new DietaryTag { DietaryTagId = Guid.NewGuid(), Name = "Spicy" }
        };
    }

    private static List<MenuItemDietaryTag> AssignDietaryTagsToMenuItems(
        Faker faker,
        List<MenuItem> menuItems,
        List<DietaryTag> dietaryTags)
    {
        var menuItemDietaryTags = new List<MenuItemDietaryTag>();
        foreach (var menuItem in menuItems)
        {
            var tagCount = faker.Random.Int(0, 3);
            if (tagCount > 0)
            {
                var selectedTags = faker.PickRandom(dietaryTags, tagCount).ToList();
                foreach (var tag in selectedTags)
                {
                    menuItemDietaryTags.Add(new MenuItemDietaryTag
                    {
                        MenuItemId = menuItem.MenuItemId,
                        DietaryTagId = tag.DietaryTagId
                    });
                }
            }
        }
        return menuItemDietaryTags;
    }
    
    private static void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
    {
        using (var hmac = new HMACSHA512())
        {
            passwordSalt = hmac.Key;
            passwordHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
        }
    }
}