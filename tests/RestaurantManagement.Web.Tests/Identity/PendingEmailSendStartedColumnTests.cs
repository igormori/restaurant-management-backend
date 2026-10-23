using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Identity.Data;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Emails without a code (account locked) mark "send started" in their own column, so the code-issued
/// column keeps meaning "a code was stored" for password reset and verification.
/// </summary>
public class PendingEmailSendStartedColumnTests : IClassFixture<PostgresDatabaseFixture>
{
    private const string SendStartedColumnSql = """
        SELECT count(*)::text
        FROM information_schema.columns
        WHERE table_name = 'pending_emails' AND column_name = 'send_started_at' AND is_nullable = 'YES'
        """;

    private readonly PostgresDatabaseFixture _fixture;

    public PendingEmailSendStartedColumnTests(PostgresDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    [Trait("Rule", "D33")]
    public async Task Migrate_CleanDatabase_PendingEmailsHasANullableSendStartedAtColumn()
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
        command.CommandText = SendStartedColumnSql;
        var columnCount = (string)(await command.ExecuteScalarAsync())!;

        // Assert
        columnCount.ShouldBe("1", "account-locked emails mark the start of the send in their own nullable column");
    }
}
