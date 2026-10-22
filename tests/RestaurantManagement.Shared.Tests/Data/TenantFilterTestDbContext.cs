using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Data.Entities;
using RestaurantManagement.Shared.Data.Tenancy;

namespace RestaurantManagement.Shared.Tests.Data;

/// <summary>A location-owned row: grams of an ingredient counted at one location.</summary>
public class TestStockCount : LocationEntity
{
    public string Ingredient { get; set; } = null!;
    public int QuantityInGrams { get; set; }
}

/// <summary>
/// A module-like DbContext that applies the tenant query filters the same way Menu and Organization do.
/// </summary>
public class TenantFilterTestDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public TenantFilterTestDbContext(DbContextOptions<TenantFilterTestDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<TestDish> Dishes => Set<TestDish>();
    public DbSet<TestStockCount> StockCounts => Set<TestStockCount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyTenantQueryFilters(() => _tenantContext);
    }
}

/// <summary>
/// SQLite in-memory database kept open for the lifetime of the instance. Rows are seeded with
/// their organization and location already set, so no save interceptor is involved.
/// </summary>
public sealed class TenantFilterTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TenantFilterTestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var context = new TenantFilterTestDbContext(Options(), Substitute.For<ITenantContext>());
        context.Database.EnsureCreated();
    }

    /// <summary>A context whose tenant is built like production: scope first, then token, plus the selected location.</summary>
    public TenantFilterTestDbContext CreateContext(Guid? tokenOrganizationId, Guid? selectedLocationId = null)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.OrganizationId.Returns(tokenOrganizationId);
        var selectedLocation = new SelectedLocation { LocationId = selectedLocationId };
        var tenantContext = new TenantContext(new TenantScope(), currentUser, selectedLocation);

        return new TenantFilterTestDbContext(Options(), tenantContext);
    }

    public async Task SeedAsync(params object[] rows)
    {
        using var context = new TenantFilterTestDbContext(Options(), Substitute.For<ITenantContext>());
        context.AddRange(rows);
        await context.SaveChangesAsync();
    }

    private DbContextOptions<TenantFilterTestDbContext> Options()
        => new DbContextOptionsBuilder<TenantFilterTestDbContext>().UseSqlite(_connection).Options;

    public void Dispose() => _connection.Dispose();
}
