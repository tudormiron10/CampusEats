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

        // context.Database.EnsureCreated(); // <-- 2. ELIMINAT (foarte important)

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
        var adminUser = new User
        {
            UserId = Guid.Parse("a0000000-0000-0000-0000-000000000001"), // ID static pentru testare
            Name = "Admin",
            Email = "admin@campuseats.com",
            Role = UserRole.Admin,
            PasswordHash = defaultHash, // <-- 3. ADĂUGAT
            PasswordSalt = defaultSalt  // <-- 3. ADĂUGAT
        };
        
        // --- 2. Articole de Meniu (Bogus) ---
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

        var fakeMenuItems = menuItemFaker.Generate(20);

        // --- 3. Clienți (Bogus) ---
        var clientUserFaker = new Faker<User>("ro")
            .RuleFor(u => u.UserId, f => Guid.NewGuid())
            .RuleFor(u => u.Name, f => f.Name.FullName())
            .RuleFor(u => u.Email, (f, u) => f.Internet.Email(u.Name))
            .RuleFor(u => u.Role, UserRole.Client)
            .RuleFor(u => u.PasswordHash, f => defaultHash) // <-- 3. ADĂUGAT
            .RuleFor(u => u.PasswordSalt, f => defaultSalt); // <-- 3. ADĂUGAT
        
        var fakeClients = clientUserFaker.Generate(4);

        // --- 4. Conturi de Loialitate (Bogus) ---
        var fakeLoyalties = new List<Loyalty>();
        foreach (var client in fakeClients)
        {
            fakeLoyalties.Add(new Loyalty
            {
                LoyaltyId = Guid.NewGuid(),
                UserId = client.UserId,
                CurrentPoints = faker.Random.Int(0, 500),
                LifetimePoints = faker.Random.Int(0, 500)
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
                    OrderDate = faker.Date.Past(1).ToUniversalTime(),
                    Status = orderStatus,
                    TotalAmount = 0 // va fi calculat mai târziu
                };
                fakeOrders.Add(newOrder);

                var orderItems = new List<OrderItem>();

                // Creăm OrderItems pentru fiecare articol din comandă
                foreach (var menuItem in itemsForOrder)
                {
                    orderItems.Add(new OrderItem
                    {
                        OrderId = newOrder.OrderId,
                        MenuItemId = menuItem.MenuItemId,
                        Quantity = faker.Random.Int(1, 3),
                        UnitPrice = menuItem.Price
                    });
                }

                // Adăugăm OrderItems în context
                context.OrderItems.AddRange(orderItems);

                // Calculăm suma totală a comenzii
                newOrder.TotalAmount = orderItems.Sum(oi => oi.Quantity * oi.UnitPrice);

                // Dacă comanda este finalizată, creăm și o plată reușită
                if (newOrder.Status == OrderStatus.Completed)
                {
                    fakePayments.Add(new Payment
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

        // --- 6. Categorii (Sample Categories) ---
        var categories = new List<Category>
        {
            new Category { CategoryId = Guid.NewGuid(), Name = "Supe", Icon = "soup", SortOrder = 1 },
            new Category { CategoryId = Guid.NewGuid(), Name = "Salate", Icon = "salad", SortOrder = 2 },
            new Category { CategoryId = Guid.NewGuid(), Name = "Fel Principal", Icon = "utensils", SortOrder = 3 },
            new Category { CategoryId = Guid.NewGuid(), Name = "Desert", Icon = "cake", SortOrder = 4 },
            new Category { CategoryId = Guid.NewGuid(), Name = "Băuturi", Icon = "cup-soda", SortOrder = 5 }
        };

        // --- 7. Dietary Tags ---
        var dietaryTags = new List<DietaryTag>
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
        var menuItemDietaryTags = new List<MenuItemDietaryTag>();
        foreach (var menuItem in fakeMenuItems)
        {
            // Randomly assign 0-3 dietary tags to each menu item
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
        context.MenuItemDietaryTags.AddRange(menuItemDietaryTags);

        context.SaveChanges();
    }
    
    // --- 4. ADĂUGAT: METODA AJUTĂTOARE DE HASHING ---
    private static void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
    {
        using (var hmac = new HMACSHA512())
        {
            passwordSalt = hmac.Key;
            passwordHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
        }
    }
}