using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Identity.Data;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// A database built only from the migrations must not ship with any account, so no default
/// credentials exist in a new deployment.
/// </summary>
public class FreshDatabaseHasNoUsersTests : IClassFixture<PostgresDatabaseFixture>
{
    private readonly PostgresDatabaseFixture _fixture;

    public FreshDatabaseHasNoUsersTests(PostgresDatabaseFixture fixture) => _fixture = fixture;

    // Matrix T24
    [Fact]
    [Trait("Rule", "T24")]
    public async Task Migrate_CleanDatabase_HasNoUsersOrRoles()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(await _fixture.CreateDatabaseAsync())
            .UseSnakeCaseNamingConvention()
            .Options;
        await using var context = new IdentityDbContext(options);

        // Act
        await context.Database.MigrateAsync();

        // Assert
        (await context.Users.CountAsync()).ShouldBe(0, "a freshly migrated database has no default accounts");
        (await context.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM user_roles").SingleAsync())
            .ShouldBe(0, "a freshly migrated database has no default role assignments");
    }
}
