using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Identity.Data;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Storage for password reset codes on a real PostgreSQL database (plan T3): its own table with the
/// same shape as verification codes, and two reset counters on users, kept apart from login and
/// verification counters. The delete rule (codes go with their user) is checked in IdentityDeleteRulesTests.
/// </summary>
public class PasswordResetCodesTableTests : IClassFixture<PostgresDatabaseFixture>
{
    private const string ResetColumnsSql = """
        SELECT coalesce(string_agg(table_name || '.' || column_name, ',' ORDER BY table_name, column_name), '')
        FROM information_schema.columns
        WHERE table_name = 'password_reset_codes'
           OR (table_name = 'users' AND column_name LIKE 'password_reset%')
        """;
    private const string ResetCodeIndexesSql = """
        SELECT coalesce(string_agg(indexname, ',' ORDER BY indexname), '')
        FROM pg_indexes
        WHERE tablename = 'password_reset_codes'
        """;

    private readonly PostgresDatabaseFixture _fixture;

    public PasswordResetCodesTableTests(PostgresDatabaseFixture fixture) => _fixture = fixture;

    private async Task<IdentityDbContext> CreateFullyMigratedContextAsync()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(await _fixture.CreateDatabaseAsync())
            .UseSnakeCaseNamingConvention()
            .Options;
        var context = new IdentityDbContext(options);
        await context.Database.MigrateAsync();
        return context;
    }

    private static async Task<string> ReadScalarAsync(DbContext context, string sql)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State == System.Data.ConnectionState.Closed)
            await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (string)(await command.ExecuteScalarAsync())!;
    }

    // Scenario 5
    [Fact]
    [Trait("Rule", "D4")]
    public async Task Migrate_CleanDatabase_CreatesResetCodeTableIndexAndUserCounters()
    {
        // Arrange
        await using var context = await CreateFullyMigratedContextAsync();

        // Act
        var columns = await ReadScalarAsync(context, ResetColumnsSql);
        var indexes = await ReadScalarAsync(context, ResetCodeIndexesSql);

        // Assert
        columns.ShouldBe(
            "password_reset_codes.code_hash,password_reset_codes.created_at,password_reset_codes.expires_at,"
            + "password_reset_codes.id,password_reset_codes.is_used,password_reset_codes.user_id,"
            + "users.password_reset_failed_attempts,users.password_reset_locked_until");
        indexes.ShouldBe("ix_password_reset_codes_user_id,pk_password_reset_codes");
    }
}
