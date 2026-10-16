using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Delete rules between users and their roles and verification codes, enforced by PostgreSQL.
/// Deletes use raw SQL on purpose: the database must enforce them whatever the write path.
/// </summary>
public class IdentityDeleteRulesTests : IClassFixture<PostgresDatabaseFixture>
{
    private const string RestrictMigrationSuffix = "_RestrictUserRoleCascadeDelete";
    private const string AuditMigrationSuffix = "_AddAuditFields";

    private const string UserRowsSql = "SELECT coalesce(json_agg(row_to_json(t) ORDER BY t.id)::text, '[]') FROM users t";
    private const string RoleRowsSql = "SELECT coalesce(json_agg(row_to_json(t) ORDER BY t.id)::text, '[]') FROM user_roles t";
    private const string CodeRowsSql = "SELECT coalesce(json_agg(row_to_json(t) ORDER BY t.id)::text, '[]') FROM user_verification_codes t";
    private const string DeleteRulesSql = """
        SELECT coalesce(string_agg(constraint_name || ':' || delete_rule, ',' ORDER BY constraint_name), '')
        FROM information_schema.referential_constraints
        """;

    private static readonly Guid CantinaId = Guid.Parse("c1c1c1c1-0000-4000-8000-000000000001");
    private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid JoaoId = Guid.Parse("22222222-bbbb-4bbb-8bbb-00000000000b");
    private static readonly Guid MariaOwnerRoleId = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001");
    private static readonly Guid JoaoStaffRoleId = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000002");
    private static readonly Guid MariaFirstCodeId = Guid.Parse("bbbbbbbb-0000-4000-8000-000000000001");
    private static readonly Guid MariaSecondCodeId = Guid.Parse("bbbbbbbb-0000-4000-8000-000000000002");
    private static readonly Guid JoaoCodeId = Guid.Parse("bbbbbbbb-0000-4000-8000-000000000003");

    private static readonly DateTime CreationTime = new(2026, 3, 14, 18, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime CodeExpiry = new(2026, 3, 14, 18, 45, 0, DateTimeKind.Utc);

    private readonly PostgresDatabaseFixture _fixture;

    public IdentityDeleteRulesTests(PostgresDatabaseFixture fixture) => _fixture = fixture;

    private async Task<DbContextOptions<IdentityDbContext>> CreateOptionsAsync()
        => new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(await _fixture.CreateDatabaseAsync())
            .UseSnakeCaseNamingConvention()
            .Options;

    private static string FindMigration(DbContext context, string suffix)
    {
        var matches = context.Database.GetMigrations().Where(m => m.EndsWith(suffix)).ToList();
        matches.Count.ShouldBe(1, $"exactly one {suffix.TrimStart('_')} migration must exist");
        return matches[0];
    }

    /// <summary>Requires the restrict migration to exist, then applies every migration.</summary>
    private async Task<IdentityDbContext> CreateFullyMigratedContextAsync()
    {
        var context = new IdentityDbContext(await CreateOptionsAsync());
        FindMigration(context, RestrictMigrationSuffix);
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

    private static async Task InsertUserAsync(DbContext context, Guid id, string email, string firstName, bool isActive)
        => await context.Database.ExecuteSqlAsync($"""
            INSERT INTO users (id, email, first_name, last_name, password_hash, failed_attempts, is_active, is_verified, verification_failed_attempts, created_at, updated_at)
            VALUES ({id}, {email}, {firstName}, 'Souza', 'hash-placeholder', 0, {isActive}, true, 0, {CreationTime}, {CreationTime})
            """);

    private static async Task InsertRoleAsync(DbContext context, Guid id, Guid userId, string role)
        => await context.Database.ExecuteSqlAsync($"""
            INSERT INTO user_roles (id, user_id, organization_id, location_id, role, created_at, updated_at, created_by_user_id, updated_by_user_id)
            VALUES ({id}, {userId}, {CantinaId}, null, {role}, {CreationTime}, {CreationTime}, {MariaId}, null)
            """);

    /// <summary>
    /// Inserts a code through EF Core without a code value, so it works whatever the code
    /// column is called (code before the hashing change, code_hash after it).
    /// </summary>
    private static async Task InsertCodeAsync(IdentityDbContext context, Guid id, Guid userId, bool isUsed)
    {
        context.UserVerificationCodes.Add(new UserVerificationCode
        {
            Id = id,
            UserId = userId,
            ExpiresAt = CodeExpiry,
            IsUsed = isUsed,
            CreatedAt = CreationTime
        });
        await context.SaveChangesAsync();
    }

    /// <summary>Inserts a readable code on a schema from before the hashing change (column code).</summary>
    private static async Task InsertReadableCodeAsync(DbContext context, Guid id, Guid userId, string code, bool isUsed)
        => await context.Database.ExecuteSqlAsync($"""
            INSERT INTO user_verification_codes (id, user_id, code, expires_at, is_used, created_at)
            VALUES ({id}, {userId}, {code}, {CodeExpiry}, {isUsed}, {CreationTime})
            """);

    // Scenario 7
    [Fact]
    [Trait("Rule", "T12")]
    public async Task DeleteUser_HasRole_IsRefusedAndRoleKept()
    {
        // Arrange
        await using var context = await CreateFullyMigratedContextAsync();
        await InsertUserAsync(context, MariaId, "maria.souza@cantina.com.br", "Maria", isActive: true);
        await InsertRoleAsync(context, MariaOwnerRoleId, MariaId, "Owner");

        // Act
        var exception = await Should.ThrowAsync<PostgresException>(
            () => context.Database.ExecuteSqlAsync($"DELETE FROM users WHERE id = {MariaId}"));

        // Assert
        exception.SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation);
        (await context.UserRoles.SingleAsync()).Id.ShouldBe(MariaOwnerRoleId);
    }

    // Scenario 8
    [Fact]
    [Trait("Rule", "T12")]
    public async Task DeleteUser_DeactivatedUserHasRole_IsRefusedAndRoleKept()
    {
        // Arrange
        await using var context = await CreateFullyMigratedContextAsync();
        await InsertUserAsync(context, JoaoId, "joao.lima@cantina.com.br", "João", isActive: false);
        await InsertRoleAsync(context, JoaoStaffRoleId, JoaoId, "Staff");

        // Act
        var exception = await Should.ThrowAsync<PostgresException>(
            () => context.Database.ExecuteSqlAsync($"DELETE FROM users WHERE id = {JoaoId}"));

        // Assert
        exception.SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation);
        (await context.UserRoles.SingleAsync()).Id.ShouldBe(JoaoStaffRoleId);
    }

    // Scenario 9
    [Fact]
    [Trait("Rule", "T12")]
    public async Task DeleteUser_HasVerificationCodesAndNoRoles_UserAndCodesDeleted()
    {
        // Arrange
        await using var context = await CreateFullyMigratedContextAsync();
        await InsertUserAsync(context, MariaId, "maria.souza@cantina.com.br", "Maria", isActive: true);
        await InsertCodeAsync(context, MariaFirstCodeId, MariaId, isUsed: true);
        await InsertCodeAsync(context, MariaSecondCodeId, MariaId, isUsed: false);

        // Act
        var deleted = await context.Database.ExecuteSqlAsync($"DELETE FROM users WHERE id = {MariaId}");

        // Assert
        deleted.ShouldBe(1);
        (await context.UserVerificationCodes.CountAsync()).ShouldBe(0);
    }

    // Scenario 10
    [Fact]
    [Trait("Rule", "T12")]
    public async Task DeleteUser_AnotherUserHasVerificationCode_OtherUsersCodeKept()
    {
        // Arrange
        await using var context = await CreateFullyMigratedContextAsync();
        await InsertUserAsync(context, MariaId, "maria.souza@cantina.com.br", "Maria", isActive: true);
        await InsertUserAsync(context, JoaoId, "joao.lima@cantina.com.br", "João", isActive: true);
        await InsertCodeAsync(context, MariaFirstCodeId, MariaId, isUsed: false);
        await InsertCodeAsync(context, JoaoCodeId, JoaoId, isUsed: false);

        // Act
        await context.Database.ExecuteSqlAsync($"DELETE FROM users WHERE id = {MariaId}");

        // Assert
        var remaining = await context.UserVerificationCodes.SingleAsync();
        remaining.Id.ShouldBe(JoaoCodeId);
        remaining.UserId.ShouldBe(JoaoId);
    }

    // Scenario 11; refresh-tokens-own-table Scenario 4 adds the refresh token cascade;
    // password reset Scenario 6 adds the reset code cascade
    [Fact]
    [Trait("Rule", "D8")]
    public async Task Migrate_IdentityForeignKeys_RolesRestrictCodesResetCodesAndRefreshTokensCascade()
    {
        // Arrange
        await using var context = await CreateFullyMigratedContextAsync();

        // Act
        var deleteRules = await ReadScalarAsync(context, DeleteRulesSql);

        // Assert
        deleteRules.ShouldBe(
            "fk_password_reset_codes_users_user_id:CASCADE,"
            + "fk_refresh_tokens_users_user_id:CASCADE,"
            + "fk_user_roles_users_user_id:RESTRICT,"
            + "fk_user_verification_codes_users_user_id:CASCADE");
    }

    // Scenario 12
    [Fact]
    [Trait("Rule", "D9")]
    public async Task Migrate_IdentityTablesWithRows_NoRowAddedChangedOrRemoved()
    {
        // Arrange
        await using var context = new IdentityDbContext(await CreateOptionsAsync());
        var restrictMigration = FindMigration(context, RestrictMigrationSuffix);
        await context.GetService<IMigrator>().MigrateAsync(FindMigration(context, AuditMigrationSuffix));
        await InsertUserAsync(context, MariaId, "maria.souza@cantina.com.br", "Maria", isActive: true);
        await InsertUserAsync(context, JoaoId, "joao.lima@cantina.com.br", "João", isActive: false);
        await InsertRoleAsync(context, MariaOwnerRoleId, MariaId, "Owner");
        await InsertRoleAsync(context, JoaoStaffRoleId, JoaoId, "Staff");
        await InsertReadableCodeAsync(context, MariaFirstCodeId, MariaId, "482913", isUsed: true);
        await InsertReadableCodeAsync(context, JoaoCodeId, JoaoId, "915274", isUsed: false);
        var usersBefore = await ReadScalarAsync(context, UserRowsSql);
        var rolesBefore = await ReadScalarAsync(context, RoleRowsSql);
        var codesBefore = await ReadScalarAsync(context, CodeRowsSql);

        // Act
        await context.GetService<IMigrator>().MigrateAsync(restrictMigration);

        // Assert
        (await ReadScalarAsync(context, UserRowsSql)).ShouldBe(usersBefore);
        (await ReadScalarAsync(context, RoleRowsSql)).ShouldBe(rolesBefore);
        (await ReadScalarAsync(context, CodeRowsSql)).ShouldBe(codesBefore);
    }
}
