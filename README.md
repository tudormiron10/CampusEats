# CampusEats

A cafeteria ordering system built with .NET 8 Minimal API and Blazor WebAssembly.

## Tech Stack

**Backend:**
- .NET 8.0 Minimal API with MediatR (CQRS)
- PostgreSQL with Entity Framework Core
- Vertical Slice Architecture
- FluentValidation
- JWT Authentication
- SignalR for real-time WebSocket communication

**Frontend:**
- Blazor WebAssembly
- TailwindCSS (custom theme)
- SignalR Client for real-time updates
- ApexCharts for analytics
- Blazored.LocalStorage

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [PostgreSQL](https://www.postgresql.org/download/) (version 12 or higher)
- IDE: Visual Studio 2022, JetBrains Rider, or VS Code

## Getting Started

### 1. Clone the Repository

```bash
git clone <your-repository-url>
cd CampusEats
```

### 2. Set Up PostgreSQL Database

Make sure PostgreSQL is running on your machine. The default connection expects:
- Host: `localhost`
- Port: `5432`
- Database: `CampusEats`
- Username: `postgres`
- Password: (your PostgreSQL password)

### 3. Configure the API

#### Option A: Using appsettings.Development.json (Simple)

Create `CampusEats.Api/appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=CampusEats;Username=postgres;Password=YOUR_PASSWORD"
  },
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:5158"
    ]
  }
}
```

**Note:** This file is gitignored for security. Replace `YOUR_PASSWORD` with your actual PostgreSQL password.

#### Option B: Using User Secrets (Recommended for Production)

```bash
cd CampusEats.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=CampusEats;Username=postgres;Password=YOUR_PASSWORD"
```

Then create `CampusEats.Api/appsettings.Development.json` without the connection string:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": ""
  },
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:5158"
    ]
  }
}
```

### 4. Configure the Client

Create `CampusEats.Client/wwwroot/appsettings.Development.json`:

```json
{
  "ApiSettings": {
    "BaseUrl": "http://localhost:5233"
  }
}
```

**Note:** This file is also gitignored. The port `5233` should match your API's HTTP port.

### 5. Run Database Migrations

```bash
cd CampusEats.Api
dotnet ef database update
```

This will create the database and seed it with sample data.

### 6. Run the Application

Open two terminal windows:

**Terminal 1 - API:**
```bash
cd CampusEats.Api
dotnet run
```

The API will start at:
- HTTP: `http://localhost:5233`
- Swagger UI: `http://localhost:5233/swagger`

**Terminal 2 - Client:**
```bash
cd CampusEats.Client
dotnet run
```

The client will start at:
- HTTP: `http://localhost:5158`

### 7. Access the Application

Open your browser and navigate to `http://localhost:5158`

## Project Structure

```
CampusEats/
├── CampusEats.Api/              # Backend API
│   ├── Features/                # Vertical slices (Menu, Orders, Payments, Kitchen, Users)
│   │   ├── Menu/
│   │   ├── Order/
│   │   ├── Payments/
│   │   ├── Kitchen/
│   │   └── User/
│   ├── Infrastructure/
│   │   └── Persistence/         # EF Core DbContext and entities
│   ├── Validators/              # FluentValidation validators
│   └── Program.cs               # API configuration and endpoints
│
└── CampusEats.Client/           # Blazor WebAssembly frontend
    ├── Pages/                   # Razor pages (Home, Menu, Cart, Orders, Kitchen)
    ├── Components/              # Reusable components (modals, drag-drop, icons)
    ├── Layout/                  # App layout and navigation
    ├── Services/                # HTTP services for API calls
    ├── Models/                  # DTOs matching API responses
    └── wwwroot/                 # Static assets and config
```

## Features

### Client Features
- Browse menu items with category filtering and dietary tag search
- Add items to cart with real-time quantity updates
- Place orders (JWT authentication required)
- View order history with expandable item details
- Cancel pending orders
- **Real-time order status updates via SignalR WebSockets**
- Order status tracking (Pending → In Preparation → Ready → Completed)
- Audio notifications when order status changes

### Staff Features (Manager/Admin)
- Full menu CRUD with image upload
- Category management with Lucide icons
- Drag-drop reordering for menu items (per-category) and categories
- Dietary tags management (multi-select)
- Kitchen dashboard with order workflow
- **Real-time new order notifications with audio alerts**
- **Live order cancellation updates**
- Analytics dashboard with charts (ApexCharts):
  - Revenue/orders over time
  - Peak hours and best days
  - Top selling items
  - Category breakdown
  - Customer insights

### Roles
- **Client:** Regular customer - browse menu, place orders, view own orders
- **Manager:** Kitchen operator - all Client abilities + menu/category CRUD, kitchen operations, analytics
- **Admin:** System administrator - all Manager abilities + user management (future: CMS dashboard)

## API Endpoints

### Menu
- `GET /menu` - Get menu items (with optional filtering)
- `GET /menu/{id}` - Get specific menu item
- `POST /menu` - Create menu item
- `PUT /menu/{id}` - Update menu item
- `DELETE /menu/{id}` - Delete menu item
- `PATCH /menu/reorder` - Reorder menu items (per-category)

### Categories
- `GET /categories` - Get all categories
- `POST /categories` - Create category
- `PUT /categories/{id}` - Update category
- `DELETE /categories/{id}` - Delete category
- `PATCH /categories/reorder` - Reorder categories

### Orders
- `GET /orders` - Get all orders
- `GET /orders/{id}` - Get specific order
- `POST /orders` - Create order
- `DELETE /orders/{id}` - Cancel order

### Payments
- `POST /payments` - Initiate payment
- `GET /payments/user/{userId}` - Get payment history
- `POST /payments/confirmation` - Payment webhook

### Kitchen
- `GET /kitchen/orders` - Get active orders (Pending/InPreparation/Ready)
- `POST /kitchen/orders/{id}/prepare` - Mark as preparing
- `POST /kitchen/orders/{id}/ready` - Mark as ready
- `POST /kitchen/orders/{id}/complete` - Mark as completed
- `GET /kitchen/report?date=` - Daily sales report
- `GET /kitchen/analytics?startDate=&endDate=&groupBy=` - Analytics data

### Users
- `POST /users/login` - JWT authentication
- `GET /users` - Get all users
- `GET /users/{id}` - Get specific user
- `POST /users` - Create user
- `PUT /users/{id}` - Update user

## Development

### Adding a New Feature

1. Create feature folder in `Features/` with subfolders: `Request/`, `Handler/`, `Response/`
2. Define request/response models
3. Create validator in `Validators/{FeatureName}/`
4. Implement handler with DbContext injection
5. Register handler in `Program.cs` services
6. Map endpoint in `Program.cs`

### Database Migrations

```bash
# Create migration
dotnet ef migrations add MigrationName --project CampusEats.Api

# Apply migration
dotnet ef database update --project CampusEats.Api

# Remove last migration
dotnet ef migrations remove --project CampusEats.Api
```

### Testing with Swagger

Visit `http://localhost:5233/swagger` to test API endpoints interactively.

## Configuration Files

The following files contain environment-specific settings and are excluded from git:

- `CampusEats.Api/appsettings.Development.json`
- `CampusEats.Client/wwwroot/appsettings.Development.json`
- `CampusEats.Client/wwwroot/appsettings.Production.json`

Make sure to create these files locally based on the examples above.

## Troubleshooting

### Port Conflicts

If ports `5233` (API) or `5158` (Client) are already in use, you can change them in:
- API: `CampusEats.Api/Properties/launchSettings.json`
- Client: `CampusEats.Client/Properties/launchSettings.json`

Update the corresponding config files to match.

### Database Connection Issues

1. Verify PostgreSQL is running: `psql -U postgres -c "SELECT version();"`
2. Check your connection string in `appsettings.Development.json` or User Secrets
3. Ensure the database exists or run migrations to create it

### CORS Errors

Make sure the `AllowedOrigins` in `CampusEats.Api/appsettings.Development.json` matches the client's URL.

## License

This project is for educational purposes.