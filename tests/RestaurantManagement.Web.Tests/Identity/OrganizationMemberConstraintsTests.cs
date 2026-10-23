using Microsoft.EntityFrameworkCore;
using Npgsql;
using RestaurantManagement.Modules.Identity.Data;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Rules on memberships that only PostgreSQL enforces: a user has one membership per organization,
/// and every membership has a real organization. Inserts use raw SQL on purpose: the database must
/// enforce the rules whatever the write path, and an empty organization cannot be expressed
/// through the entity.
/// </summary>
public class OrganizationMemberConstraintsTests : IClassFixture<PostgresDatabaseFixture>
{
    private static readonly Guid CantinaId = Guid.Parse("c1c1c1c1-0000-4000-8000-000000000001");
    private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid FirstMemberId = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001");
    private static readonly Guid SecondMemberId = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000002");

    private static readonly DateTime CreationTime = new(2026, 10, 20, 18, 30, 0, DateTimeKind.Utc);

    private readonly PostgresDatabaseFixture _fixture;

    public OrganizationMemberConstraintsTests(PostgresDatabaseFixture fixture) => _fixture = fixture;

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
            INSERT INTO users (id, email, first_name, last_name, password_hash, is_active, is_verified, verification_failed_attempts, created_at, updated_at)
            VALUES ({MariaId}, 'maria.souza@cantina.com.br', 'Maria', 'Souza', 'hash-placeholder', true, true, 0, {CreationTime}, {CreationTime})
            """);
        return context;
    }

    private static Task<int> InsertMemberAsync(DbContext context, Guid id, Guid organizationId, bool isOwner)
        => context.Database.ExecuteSqlAsync($"""
            INSERT INTO organization_members (id, user_id, organization_id, is_owner, created_at, updated_at, created_by_user_id, updated_by_user_id)
            VALUES ({id}, {MariaId}, {organizationId}, {isOwner}, {CreationTime}, {CreationTime}, {MariaId}, null)
            """);

    [Fact]
    [Trait("Rule", "T13")]
    public async Task InsertMember_SecondMembershipSameUserAndOrganization_IsRejectedByUniqueIndex()
    {
        // Arrange
        await using var context = await CreateMigratedContextWithMariaAsync();
        await InsertMemberAsync(context, FirstMemberId, CantinaId, isOwner: true);

        // Act
        var exception = await Should.ThrowAsync<PostgresException>(
            () => InsertMemberAsync(context, SecondMemberId, CantinaId, isOwner: false));

        // Assert
        exception.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation, "a user is a member of an organization once, whatever the Owner flag says");
    }

    [Fact]
    [Trait("Rule", "T13")]
    public async Task InsertMember_EmptyOrganization_IsRejectedByCheck()
    {
        // Arrange
        await using var context = await CreateMigratedContextWithMariaAsync();

        // Act
        var exception = await Should.ThrowAsync<PostgresException>(
            () => InsertMemberAsync(context, FirstMemberId, Guid.Empty, isOwner: false));

        // Assert
        exception.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation, "a membership must belong to a real organization, never the empty ID");
    }
}
