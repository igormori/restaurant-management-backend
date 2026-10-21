using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using NSubstitute;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Data.AuditLog;
using RestaurantManagement.Shared.Data.Entities;
using RestaurantManagement.Shared.Data.Tenancy;

namespace RestaurantManagement.Shared.Tests;

public class TestDish : TenantEntity
{
    public string Name { get; set; } = null!;
    public decimal PriceInBrl { get; set; }
}

public class TestOrganization : BaseEntity
{
    public string Name { get; set; } = null!;
}

public class AuditTestDbContext : DbContext
{
    public AuditTestDbContext(DbContextOptions<AuditTestDbContext> options) : base(options) { }

    public DbSet<TestDish> Dishes => Set<TestDish>();
    public DbSet<TestOrganization> Organizations => Set<TestOrganization>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // The interceptor, not EF, must be what fills an empty Id.
        modelBuilder.Entity<TestDish>().Property(d => d.Id).ValueGeneratedNever();
        modelBuilder.Entity<TestOrganization>().Property(o => o.Id).ValueGeneratedNever();
    }
}

public sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;

    public FixedTimeProvider(DateTimeOffset now) => _now = now;

    public override DateTimeOffset GetUtcNow() => _now;
}

/// <summary>
/// SQLite in-memory database kept open for the lifetime of the instance. Contexts created
/// without an interceptor are for seeding and for reading back what was really written.
/// </summary>
public sealed class AuditTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public AuditTestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var context = CreatePlainContext();
        context.Database.EnsureCreated();
    }

    public AuditTestDbContext CreatePlainContext() => CreateContext(null);

    public AuditTestDbContext CreateContext(AuditSaveChangesInterceptor? interceptor)
    {
        var builder = new DbContextOptionsBuilder<AuditTestDbContext>().UseSqlite(_connection);
        if (interceptor is not null)
            builder.AddInterceptors(interceptor);

        return new AuditTestDbContext(builder.Options);
    }

    public static AuditSaveChangesInterceptor CreateInterceptor(
        DateTimeOffset now,
        Guid? userId = null,
        Guid? userOrganizationId = null,
        Guid? scopeOrganizationId = null)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(userId);
        currentUser.OrganizationId.Returns(userOrganizationId);

        var tenantScope = Substitute.For<ITenantScope>();
        tenantScope.OrganizationId.Returns(scopeOrganizationId);

        var localizer = Substitute.For<IStringLocalizer<SharedResource>>();
        localizer[Arg.Any<string>()].Returns(call => new LocalizedString(call.Arg<string>(), call.Arg<string>()));

        return new AuditSaveChangesInterceptor(currentUser, tenantScope, new FixedTimeProvider(now), localizer);
    }

    public void Dispose() => _connection.Dispose();
}
