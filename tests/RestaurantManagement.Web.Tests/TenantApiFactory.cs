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
using RestaurantManagement.Shared.Auth;
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

    public static async Task<TenantApiFactory> CreateAsync(PostgresDatabaseFixture postgres)
    {
        var factory = new TenantApiFactory(await postgres.CreateDatabaseAsync());
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<OrganizationDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<MenuDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<AuditLogDbContext>().Database.MigrateAsync();
        return factory;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
        // Keep test output readable: SQL and startup logs are not what these tests check.
        builder.ConfigureLogging(logging => logging.ClearProviders());
    }

    /// <summary>
    /// A client signed in as the user in the organization. The pass holds no role: what the user
    /// may do comes from the access grid stored with <see cref="SeedUserAsync"/> and <see cref="GrantAsync"/>.
    /// </summary>
    public HttpClient ClientFor(Guid userId, Guid? organizationId, string? locationHeader = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestAccessTokens.Create(Services, userId, organizationId));
        if (locationHeader is not null)
            client.DefaultRequestHeaders.Add(LocationHeader, locationHeader);
        return client;
    }

    public string Message(string key)
        => Services.GetRequiredService<IStringLocalizer<SharedResource>>()[key].Value;

    /// <summary>
    /// Stores a verified user and the given memberships (an Owner is a membership with the Owner
    /// flag). A member who is not the Owner has no access until <see cref="GrantAsync"/> gives it.
    /// </summary>
    public async Task SeedUserAsync(Guid userId, string email, string firstName, params OrganizationMember[] memberships)
    {
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            db.Users.Add(new User
            {
                Id = userId,
                Email = email,
                PasswordHash = "not-used-by-token-signed-requests",
                FirstName = firstName,
                LastName = "Silva",
                IsVerified = true
            });
            await db.SaveChangesAsync();
        }

        foreach (var membership in memberships)
            await AddMembershipAsync(userId, membership);
    }

    /// <summary>Stores a membership for a user that already exists, inside a tenant scope for its organization.</summary>
    public async Task AddMembershipAsync(Guid userId, OrganizationMember membership)
    {
        membership.UserId = userId;
        await InOrganizationAsync<IdentityDbContext>(membership.OrganizationId, async db =>
        {
            db.Set<OrganizationMember>().Add(membership);
            await db.SaveChangesAsync();
        });
    }

    /// <summary>A membership of the organization who is not the Owner (no access until granted).</summary>
    public static OrganizationMember MemberOf(Guid organizationId) => new() { OrganizationId = organizationId };

    /// <summary>A membership of the organization with the Owner flag (full access, never restricted).</summary>
    public static OrganizationMember OwnerOf(Guid organizationId) => new() { OrganizationId = organizationId, IsOwner = true };

    /// <summary>
    /// Stores the member's levels at one location: one stored cell per module and level, exactly as
    /// the access grid keeps them (a level of None is no row).
    /// </summary>
    public async Task GrantAsync(Guid userId, Guid organizationId, Guid locationId, params (AccessModule Module, AccessLevel Level)[] levels)
    {
        await InOrganizationAsync<IdentityDbContext>(organizationId, async db =>
        {
            foreach (var (module, level) in levels)
                db.Set<MemberLocationLevel>().Add(new MemberLocationLevel
                {
                    UserId = userId,
                    OrganizationId = organizationId,
                    LocationId = locationId,
                    Module = module,
                    Level = level
                });
            await db.SaveChangesAsync();
        });
    }

    /// <summary>Changes one stored cell, as a later grid change would (the next request sees it); None removes the cell.</summary>
    public async Task SetLevelAsync(Guid userId, Guid organizationId, Guid locationId, AccessModule module, AccessLevel level)
    {
        await InOrganizationAsync<IdentityDbContext>(organizationId, async db =>
        {
            var cell = await db.Set<MemberLocationLevel>().SingleAsync(c =>
                c.UserId == userId && c.OrganizationId == organizationId && c.LocationId == locationId && c.Module == module);
            if (level == AccessLevel.None)
                db.Set<MemberLocationLevel>().Remove(cell);
            else
                cell.Level = level;
            await db.SaveChangesAsync();
        });
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

    /// <summary>
    /// Stores audit log entries exactly as given (organization, user and time included), through a
    /// context without the audit interceptor, so tests can place entries at chosen times.
    /// </summary>
    public async Task SeedAuditEntriesAsync(params AuditLogEntry[] entries)
    {
        var options = new DbContextOptionsBuilder<AuditLogDbContext>()
            .UseNpgsql(_connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        await using var db = new AuditLogDbContext(options, NoTenant.Context);
        db.AuditLogEntries.AddRange(entries);
        await db.SaveChangesAsync();
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
