using Microsoft.EntityFrameworkCore;
using Npgsql;
using RestaurantManagement.Modules.Identity.Data;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Rules on role assignments that only PostgreSQL enforces: every assignment has an organization,
/// and the same assignment cannot exist twice, "all locations" (empty location) included.
/// Inserts use raw SQL on purpose: the database must enforce the rules whatever the write path,
/// and an empty organization can no longer be expressed through the entity.
/// </summary>
public class UserRoleConstraintsTests : IClassFixture<AuditFieldsPostgresFixture>
{
    private static readonly Guid CantinaId = Guid.Parse("c1c1c1c1-0000-4000-8000-000000000001");
    private static readonly Guid TrattoriaId = Guid.Parse("c2c2c2c2-0000-4000-8000-000000000002");
    private static readonly Guid CentroLocationId = Guid.Parse("44444444-dddd-4ddd-8ddd-00000000000d");
    private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid FirstRoleId = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001");
    private static readonly Guid SecondRoleId = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000002");

    private static readonly DateTime CreationTime = new(2026, 10, 15, 18, 30, 0, DateTimeKind.Utc);

    private readonly AuditFieldsPostgresFixture _fixture;

    public UserRoleConstraintsTests(AuditFieldsPostgresFixture fixture) => _fixture = fixture;

    /// <summary>Applies every Identity migration and adds Maria as a user.</summary>
    private async Task<IdentityDbContext> CreateMigratedContextWithMariaAsync()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(await _fixture.CreateDatabaseAsync())
            .UseSnakeCaseNamingConvention()
            .Options;
        var context = new IdentityDbContext(options);
        await context.Database.MigrateAsync();
        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO users (id, email, first_name, last_name, password_hash, failed_attempts, is_active, is_verified, verification_failed_attempts, created_at, updated_at)
            VALUES ({MariaId}, 'maria.souza@cantina.com.br', 'Maria', 'Souza', 'hash-placeholder', 0, true, true, 0, {CreationTime}, {CreationTime})
            """);
        return context;
    }

    private static Task<int> InsertRoleAsync(DbContext context, Guid id, Guid organizationId, Guid? locationId, string role)
        => context.Database.ExecuteSqlAsync($"""
            INSERT INTO user_roles (id, user_id, organization_id, location_id, role, created_at, updated_at, created_by_user_id, updated_by_user_id)
            VALUES ({id}, {MariaId}, {organizationId}, {locationId}, {role}, {CreationTime}, {CreationTime}, {MariaId}, null)
            """);

    private static Task<int> InsertRoleWithoutOrganizationAsync(DbContext context, Guid id, string role)
        => context.Database.ExecuteSqlAsync($"""
            INSERT INTO user_roles (id, user_id, organization_id, location_id, role, created_at, updated_at, created_by_user_id, updated_by_user_id)
            VALUES ({id}, {MariaId}, null, null, {role}, {CreationTime}, {CreationTime}, {MariaId}, null)
            """);

    private static Task<int> CountRolesAsync(IdentityDbContext context)
        => context.UserRoles.CountAsync();

    // Scenario 13
    [Fact]
    public async Task InsertUserRole_NoOrganization_IsRejectedByNotNullRule()
    {
        // Arrange
        await using var context = await CreateMigratedContextWithMariaAsync();

        // Act
        var exception = await Should.ThrowAsync<PostgresException>(
            () => InsertRoleWithoutOrganizationAsync(context, FirstRoleId, "Owner"));

        // Assert
        exception.SqlState.ShouldBe(PostgresErrorCodes.NotNullViolation);
        exception.ColumnName.ShouldBe("organization_id");
    }

    // Scenario 14
    [Fact]
    public async Task InsertUserRole_SecondAllLocationsRowSameOrganizationAndRole_IsRejectedByUniqueIndex()
    {
        // Arrange
        await using var context = await CreateMigratedContextWithMariaAsync();
        await InsertRoleAsync(context, FirstRoleId, CantinaId, locationId: null, "Manager");

        // Act
        var exception = await Should.ThrowAsync<PostgresException>(
            () => InsertRoleAsync(context, SecondRoleId, CantinaId, locationId: null, "Manager"));

        // Assert
        exception.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
        exception.ConstraintName.ShouldBe(IdentityDbContext.UniqueUserRoleIndexName);
    }

    // Scenario 15
    [Fact]
    public async Task InsertUserRole_SecondRowSameSpecificLocationAndRole_IsRejectedByUniqueIndex()
    {
        // Arrange
        await using var context = await CreateMigratedContextWithMariaAsync();
        await InsertRoleAsync(context, FirstRoleId, CantinaId, CentroLocationId, "Waiter");

        // Act
        var exception = await Should.ThrowAsync<PostgresException>(
            () => InsertRoleAsync(context, SecondRoleId, CantinaId, CentroLocationId, "Waiter"));

        // Assert
        exception.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
        exception.ConstraintName.ShouldBe(IdentityDbContext.UniqueUserRoleIndexName);
    }

    // Scenario 16
    [Fact]
    public async Task InsertUserRole_SameRoleAllLocationsAndOneSpecificLocation_BothRowsKept()
    {
        // Arrange
        await using var context = await CreateMigratedContextWithMariaAsync();
        await InsertRoleAsync(context, FirstRoleId, CantinaId, locationId: null, "Manager");

        // Act
        await InsertRoleAsync(context, SecondRoleId, CantinaId, CentroLocationId, "Manager");

        // Assert
        (await CountRolesAsync(context)).ShouldBe(2);
    }

    // Scenario 17
    [Fact]
    public async Task InsertUserRole_SameOrganizationAndLocationDifferentRole_BothRowsKept()
    {
        // Arrange
        await using var context = await CreateMigratedContextWithMariaAsync();
        await InsertRoleAsync(context, FirstRoleId, CantinaId, locationId: null, "Manager");

        // Act
        await InsertRoleAsync(context, SecondRoleId, CantinaId, locationId: null, "Cashier");

        // Assert
        (await CountRolesAsync(context)).ShouldBe(2);
    }

    // Scenario 18
    [Fact]
    public async Task InsertUserRole_SameAllLocationsRoleInAnotherOrganization_BothRowsKept()
    {
        // Arrange
        await using var context = await CreateMigratedContextWithMariaAsync();
        await InsertRoleAsync(context, FirstRoleId, CantinaId, locationId: null, "Owner");

        // Act
        await InsertRoleAsync(context, SecondRoleId, TrattoriaId, locationId: null, "Owner");

        // Assert
        (await CountRolesAsync(context)).ShouldBe(2);
    }
}
