# Restaurant Management Backend

A modular monolith for restaurant management with multi-tenant support, built with .NET 9 and PostgreSQL.

## 🏗️ Architecture

**Modular Monolith** with per-module `DbContext`:
- **Identity Module** - Authentication & authorization
- **Organization Module** - Organizations & locations  
- **Menu Module** - Menu management
- **Shared** - Cross-cutting concerns (middleware, email, localization)

Services can inject multiple `DbContext` instances for cross-module data access.

## 📁 Project Structure

```
├── RestaurantManagement.Web/              # Entry point & host
├── RestaurantManagement.Shared/           # Middleware, email, localization
├── RestaurantManagement.Modules.Identity/ # Users, roles, auth
├── RestaurantManagement.Modules.Organization/ # Orgs, locations
└── RestaurantManagement.Modules.Menu/     # Menus
```

## 🚀 Quick Start

### 1. Configure Database

Edit `RestaurantManagement.Web/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=restaurant_db-development;Username=postgres;Password=your_password"
  },
  "Jwt": {
    "Key": "your-secret-key-min-32-chars"
  }
}
```

### 2. Run Migrations

```bash
# Identity
dotnet ef database update --project RestaurantManagement.Modules.Identity --startup-project RestaurantManagement.Web --context IdentityDbContext

# Organization
dotnet ef database update --project RestaurantManagement.Modules.Organization --startup-project RestaurantManagement.Web --context OrganizationDbContext

# Menu
dotnet ef database update --project RestaurantManagement.Modules.Menu --startup-project RestaurantManagement.Web --context MenuDbContext
```

### 3. Run the Application

```bash
cd RestaurantManagement.Web
dotnet run
```

**Swagger UI:** http://localhost:5159/swagger

## � API Usage

### Register & Login

```bash
# Register
POST /api/auth/register
{
  "email": "user@example.com",
  "password": "SecurePass123!",
  "firstName": "John",
  "lastName": "Doe"
}

# Login (returns JWT token)
POST /api/auth/login
{
  "email": "user@example.com",
  "password": "SecurePass123!"
}
```

### Create Organization (Authenticated)

```bash
POST /api/organizations/create
Authorization: Bearer <your-token>

{
  "name": "My Restaurant",
  "description": "A great restaurant",
  "primaryColor": "#FF5733"
}
```

**Swagger Auth:** Click 🔒 Authorize button → Paste token → Authorize

## 🔧 Development

### Create New Migration

```bash
dotnet ef migrations add MigrationName \
  --project RestaurantManagement.Modules.[ModuleName] \
  --startup-project RestaurantManagement.Web \
  --context [ModuleName]DbContext
```

### Build

```bash
dotnet build
```

## 🎯 Key Features

- ✅ Multi-tenant with trial/subscription support
- ✅ JWT authentication with refresh tokens
- ✅ Role-based access control (Owner, Admin, Staff)
- ✅ Cross-module data access via multi-DbContext injection
- ✅ Sentry error tracking
- ✅ Multi-language support (en, pt-BR)
- ✅ Email verification

## 🐛 Troubleshooting

**Port in use:**
```bash
lsof -ti:5159 | xargs kill -9
```

**Database issues:**
- Verify PostgreSQL is running
- Check connection string
- Ensure migrations are applied

## 📚 Tech Stack

- .NET 9.0
- PostgreSQL + EF Core
- JWT Authentication
- Sentry
- Swagger/OpenAPI
