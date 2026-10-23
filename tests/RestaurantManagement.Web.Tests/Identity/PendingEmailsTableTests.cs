using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Identity.Data;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Storage for queued forgot-password requests on a real PostgreSQL database: its own table with no
/// link to users (the email may belong to nobody), and an index so due requests are found quickly.
/// </summary>
public class PendingEmailsTableTests : IClassFixture<PostgresDatabaseFixture>
{
    private const string ColumnsSql = """
        SELECT coalesce(string_agg(column_name, ',' ORDER BY column_name), '')
        FROM information_schema.columns
        WHERE table_name = 'pending_emails'
        """;
    private const string IndexesSql = """
        SELECT coalesce(string_agg(indexname, ',' ORDER BY indexname), '')
        FROM pg_indexes
        WHERE tablename = 'pending_emails'
        """;
    private const string ForeignKeysSql = """
        SELECT count(*)::text
        FROM information_schema.table_constraints
        WHERE table_name = 'pending_emails' AND constraint_type = 'FOREIGN KEY'
        """;

    private const string PurposeNullableSql = """
        SELECT coalesce(max(is_nullable), '')
        FROM information_schema.columns
        WHERE table_name = 'pending_emails' AND column_name = 'purpose'
        """;

    private readonly PostgresDatabaseFixture _fixture;

    public PendingEmailsTableTests(PostgresDatabaseFixture fixture) => _fixture = fixture;

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
    [Trait("Rule", "D18")]
    public async Task Migrate_CleanDatabase_CreatesPendingEmailTableWithDueTimeIndexAndNoUserLink()
    {
        // Arrange
        await using var context = await CreateFullyMigratedContextAsync();

        // Act
        var columns = await ReadScalarAsync(context, ColumnsSql);
        var indexes = await ReadScalarAsync(context, IndexesSql);
        var foreignKeys = await ReadScalarAsync(context, ForeignKeysSql);

        // Assert
        columns.ShouldBe("attempts,claimed_until,code_issued_at,created_at,email,failed_at,id,next_attempt_at,purpose,send_started_at", "a queued request holds the email as typed, its purpose, attempts, claim, code-issued mark, failure and next attempt time");
        indexes.ShouldBe("ix_pending_emails_failed_at,ix_pending_emails_next_attempt_at,pk_pending_emails", "finding the due requests and the old failed ones must be indexed");
        foreignKeys.ShouldBe("0", "a queued request has no user, so the table must not link to users");
    }

    [Fact]
    [Trait("Rule", "D25")]
    public async Task Migrate_CleanDatabase_PurposeColumnIsRequired()
    {
        // Arrange
        await using var context = await CreateFullyMigratedContextAsync();

        // Act
        var isNullable = await ReadScalarAsync(context, PurposeNullableSql);

        // Assert
        isNullable.ShouldBe("NO", "every queued request must say whether it is for a password reset or a verification, so none can be taken for the wrong kind");
    }
}
