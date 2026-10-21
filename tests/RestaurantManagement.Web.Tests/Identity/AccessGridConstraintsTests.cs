using Microsoft.EntityFrameworkCore;
using Npgsql;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities.Members;
using RestaurantManagement.Modules.Identity.Entities.Roles;
using RestaurantManagement.Shared.Auth;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Rules on roles and stored levels that only PostgreSQL enforces: a role name is used once in an
/// organization, and a member has one level per module and location.
/// </summary>
public class AccessGridConstraintsTests : IClassFixture<PostgresDatabaseFixture>
{
    private static readonly Guid CantinaId = Guid.Parse("c1c1c1c1-0000-4000-8000-000000000001");
    private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid BatelId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000001");

    private static readonly DateTime CreationTime = new(2026, 10, 20, 18, 30, 0, DateTimeKind.Utc);

    private readonly PostgresDatabaseFixture _fixture;

    public AccessGridConstraintsTests(PostgresDatabaseFixture fixture) => _fixture = fixture;

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

    [Fact]
    [Trait("Rule", "T30")]
    public async Task SaveRole_SameNameTwiceInOneOrganization_IsRejectedByUniqueIndex()
    {
        // Arrange
        await using var context = await CreateMigratedContextWithMariaAsync();
        context.Set<OrganizationRole>().Add(new OrganizationRole { OrganizationId = CantinaId, Name = "Waiter" });
        await context.SaveChangesAsync();
        context.Set<OrganizationRole>().Add(new OrganizationRole { OrganizationId = CantinaId, Name = "Waiter" });

        // Act
        var exception = await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync());

        // Assert
        (exception.InnerException as PostgresException)?.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation, "a role name is used once in an organization");
    }

    [Fact]
    [Trait("Rule", "T30")]
    public async Task SaveRole_SameNameInAnotherLetterCaseInOneOrganization_IsRejectedByTheCaseInsensitiveUniqueIndex()
    {
        // Arrange
        await using var context = await CreateMigratedContextWithMariaAsync();
        context.Set<OrganizationRole>().Add(new OrganizationRole { OrganizationId = CantinaId, Name = "Waiter" });
        await context.SaveChangesAsync();
        context.Set<OrganizationRole>().Add(new OrganizationRole { OrganizationId = CantinaId, Name = "waiter" });

        // Act
        var exception = await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync());

        // Assert
        var postgres = exception.InnerException as PostgresException;
        postgres?.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation, "a role name is used once in an organization, whatever its letter case");
        postgres?.ConstraintName.ShouldBe(IdentityDbContext.UniqueRoleNameIndexName, "the index keeps the fixed name the services catch");
    }

    [Fact]
    [Trait("Rule", "T127")]
    public async Task SaveRole_SameNameInAnotherOrganization_IsAllowed()
    {
        // Arrange
        await using var context = await CreateMigratedContextWithMariaAsync();
        var trattoriaId = Guid.Parse("c2c2c2c2-0000-4000-8000-000000000002");
        context.Set<OrganizationRole>().Add(new OrganizationRole { OrganizationId = CantinaId, Name = "Waiter" });
        context.Set<OrganizationRole>().Add(new OrganizationRole { OrganizationId = trattoriaId, Name = "waiter" });

        // Act
        var act = () => context.SaveChangesAsync();

        // Assert
        await Should.NotThrowAsync(act, "another organization may use the same name");
    }

    [Fact]
    [Trait("Rule", "T31")]
    public async Task SaveLevel_SameCellTwice_IsRejectedByUniqueIndex()
    {
        // Arrange
        await using var context = await CreateMigratedContextWithMariaAsync();
        context.Set<MemberLocationLevel>().Add(Cell(AccessLevel.Read));
        await context.SaveChangesAsync();
        context.Set<MemberLocationLevel>().Add(Cell(AccessLevel.Manage));

        // Act
        var exception = await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync());

        // Assert
        (exception.InnerException as PostgresException)?.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation, "a member has one level per module and location, so a second row for the same cell is refused");
    }

    private static MemberLocationLevel Cell(AccessLevel level) => new()
    {
        UserId = MariaId,
        OrganizationId = CantinaId,
        LocationId = BatelId,
        Module = AccessModule.Menus,
        Level = level
    };
}
