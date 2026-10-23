using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Identity.Data;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Storage of counted wrong passwords on a real PostgreSQL database: its own table with an index by user
/// and time, rows that go with their user, and no failed-attempt counter left on users.
/// </summary>
public class LoginFailuresTableTests : IClassFixture<PostgresDatabaseFixture>
{
    private const string ColumnsSql = """
        SELECT coalesce(string_agg(column_name, ',' ORDER BY column_name), '')
        FROM information_schema.columns
        WHERE table_name = 'login_failures'
        """;
    private const string UserTimeIndexSql = """
        SELECT count(*)::text
        FROM pg_indexes
        WHERE tablename = 'login_failures' AND indexdef LIKE '%(user_id, failed_at)%'
        """;
    private const string UserDeleteRuleSql = """
        SELECT coalesce(max(rc.delete_rule), '')
        FROM information_schema.referential_constraints rc
        WHERE rc.constraint_name = 'fk_login_failures_users_user_id'
        """;
    private const string OldCounterColumnSql = """
        SELECT count(*)::text
        FROM information_schema.columns
        WHERE table_name = 'users' AND column_name = 'failed_attempts'
        """;

    private readonly PostgresDatabaseFixture _fixture;

    public LoginFailuresTableTests(PostgresDatabaseFixture fixture) => _fixture = fixture;

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

    [Fact]
    [Trait("Rule", "D29")]
    public async Task Migrate_CleanDatabase_CreatesLoginFailuresTableWithUserAndTimeIndex()
    {
        // Arrange
        await using var context = await CreateFullyMigratedContextAsync();

        // Act
        var columns = await ReadScalarAsync(context, ColumnsSql);
        var indexCount = await ReadScalarAsync(context, UserTimeIndexSql);

        // Assert
        columns.ShouldBe("address_locked_until,counts_for_account,counts_for_address,failed_at,id,ip_address,user_id", "each counted wrong password is stored with its user, address, time, two counting marks and the address lock end");
        indexCount.ShouldBe("1", "one index by user and failed-at keeps the counts fast");
    }

    [Fact]
    [Trait("Rule", "T12")]
    public async Task Migrate_CleanDatabase_LoginFailuresGoWithTheirUser()
    {
        // Arrange
        await using var context = await CreateFullyMigratedContextAsync();

        // Act
        var deleteRule = await ReadScalarAsync(context, UserDeleteRuleSql);

        // Assert
        deleteRule.ShouldBe("CASCADE", "deleting a user deletes that user's stored login failures");
    }

    [Fact]
    [Trait("Rule", "D30")]
    public async Task Migrate_CleanDatabase_UsersHasNoFailedAttemptsColumn()
    {
        // Arrange
        await using var context = await CreateFullyMigratedContextAsync();

        // Act
        var columnCount = await ReadScalarAsync(context, OldCounterColumnSql);

        // Assert
        columnCount.ShouldBe("0", "the per-account counter is replaced by stored login failures");
    }
}
