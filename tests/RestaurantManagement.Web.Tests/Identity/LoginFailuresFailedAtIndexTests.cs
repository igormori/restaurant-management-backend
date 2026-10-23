using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Identity.Data;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// The cleanup of expired login failures filters by time across all users, so the table needs an index
/// that starts with the failed-at column.
/// </summary>
public class LoginFailuresFailedAtIndexTests : IClassFixture<PostgresDatabaseFixture>
{
    private const string FailedAtIndexSql = """
        SELECT count(*)::text
        FROM pg_indexes
        WHERE tablename = 'login_failures' AND indexdef LIKE '%(failed_at)%'
        """;

    private readonly PostgresDatabaseFixture _fixture;

    public LoginFailuresFailedAtIndexTests(PostgresDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    [Trait("Rule", "D32")]
    public async Task Migrate_CleanDatabase_CreatesAnIndexOnLoginFailuresFailedAt()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(await _fixture.CreateDatabaseAsync())
            .UseSnakeCaseNamingConvention()
            .Options;
        await using var context = new IdentityDbContext(options);
        await context.Database.MigrateAsync();

        // Act
        var connection = context.Database.GetDbConnection();
        if (connection.State == System.Data.ConnectionState.Closed)
            await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = FailedAtIndexSql;
        var indexCount = (string)(await command.ExecuteScalarAsync())!;

        // Assert
        indexCount.ShouldBe("1", "one index on failed-at keeps the cleanup of expired failures from scanning the whole table");
    }
}
