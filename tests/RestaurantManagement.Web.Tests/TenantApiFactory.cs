using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Menu.Data;
using RestaurantManagement.Modules.Menu.Entities;
using RestaurantManagement.Modules.Organization.Data;
using RestaurantManagement.Modules.Organization.Entities;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Data;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// The real app (real Menu and Organization services, real DbContexts) on its own migrated PostgreSQL
/// database from the shared Testcontainers fixture. Identity's login services stay substituted as in
/// <see cref="AuthApiFactory"/>; requests sign in with <see cref="TestAccessTokens"/>.
/// Rows are seeded and read back inside a tenant scope for their organization, the way the app does.
/// </summary>
public sealed class TenantApiFactory : AuthApiFactory
{
    public const string LocationHeader = "X-Location-Id";

    private static readonly DateTime CreationTime = new(2026, 10, 15, 18, 30, 0, DateTimeKind.Utc);

    private readonly string _connectionString;

    private TenantApiFactory(string connectionString) => _connectionString = connectionString;

    public static async Task<TenantApiFactory> CreateAsync(AuditFieldsPostgresFixture postgres)
    {
        var factory = new TenantApiFactory(await postgres.CreateDatabaseAsync());
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<OrganizationDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<MenuDbContext>().Database.MigrateAsync();
        return factory;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
        // Keep test output readable: SQL and startup logs are not what these tests check.
        builder.ConfigureLogging(logging => logging.ClearProviders());
    }

    public HttpClient ClientFor(Guid userId, Guid? organizationId, string role, string? locationHeader = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestAccessTokens.Create(Services, userId, organizationId, role));
        if (locationHeader is not null)
            client.DefaultRequestHeaders.Add(LocationHeader, locationHeader);
        return client;
    }

    public string Message(string key)
        => Services.GetRequiredService<IStringLocalizer<SharedResource>>()[key].Value;

    public async Task SeedUserAsync(Guid userId, string email, string firstName, params UserRole[] roles)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var user = new User
        {
            Id = userId,
            Email = email,
            PasswordHash = "not-used-by-token-signed-requests",
            FirstName = firstName,
            LastName = "Silva",
            IsVerified = true
        };
        foreach (var role in roles)
            user.Roles.Add(role);
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    public async Task SeedOrganizationAsync(Guid organizationId, string name, Currency currency)
    {
        await InOrganizationAsync<OrganizationDbContext>(organizationId, async db =>
        {
            db.Organizations.Add(new Organization { Id = organizationId, Name = name, Currency = currency, CreatedAt = CreationTime, UpdatedAt = CreationTime });
            db.OrganizationSettings.Add(new OrganizationSettings
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                PlanType = "TRIAL",
                MaxLocations = 1,
                TrialEndDate = CreationTime.AddDays(30),
                IsTrialActive = true
            });
            await db.SaveChangesAsync();
        });
    }

    public async Task SeedLocationAsync(Guid organizationId, Guid locationId, string name, string code, string timeZone)
    {
        await InOrganizationAsync<OrganizationDbContext>(organizationId, async db =>
        {
            db.Locations.Add(new Location
            {
                Id = locationId,
                OrganizationId = organizationId,
                Name = name,
                Address = "Rua XV de Novembro, 300",
                City = "Curitiba",
                State = "PR",
                PostalCode = "80020-310",
                Country = "Brasil",
                PhoneNumber = "+55 41 3000-2000",
                Status = LocationStatus.Active,
                Code = code,
                TimeZone = timeZone
            });
            await db.SaveChangesAsync();
        });
    }

    public async Task SeedMenuAsync(Guid organizationId, Guid menuId, string name)
    {
        await InOrganizationAsync<MenuDbContext>(organizationId, async db =>
        {
            db.Menus.Add(new Menu { Id = menuId, OrganizationId = organizationId, Name = name, IsActive = true });
            await db.SaveChangesAsync();
        });
    }

    public async Task SeedCategoryAsync(Guid organizationId, Guid menuId, Guid categoryId, string name)
    {
        await InOrganizationAsync<MenuDbContext>(organizationId, async db =>
        {
            db.MenuCategories.Add(new MenuCategory { Id = categoryId, MenuId = menuId, OrganizationId = organizationId, Name = name, SortOrder = 1 });
            await db.SaveChangesAsync();
        });
    }

    /// <summary>Runs work against a fresh DbContext while a tenant scope for the organization is open.</summary>
    public async Task<T> InOrganizationAsync<TContext, T>(Guid organizationId, Func<TContext, Task<T>> work) where TContext : DbContext
    {
        using var scope = Services.CreateScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Begin(organizationId);
        return await work(scope.ServiceProvider.GetRequiredService<TContext>());
    }

    private Task InOrganizationAsync<TContext>(Guid organizationId, Func<TContext, Task> work) where TContext : DbContext
        => InOrganizationAsync<TContext, bool>(organizationId, async db =>
        {
            await work(db);
            return true;
        });
}
