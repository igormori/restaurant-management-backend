using NSubstitute;
using RestaurantManagement.Modules.Identity.Entities.Members;
using RestaurantManagement.Modules.Identity.Entities.Users;
using RestaurantManagement.Modules.Identity.Services.Passwords;
using RestaurantManagement.Shared.Data.Tenancy;
using RestaurantManagement.Shared.Services.AuditLog;
using Shouldly;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// A password reset or change is written to the audit log once per organization the user is a
    /// member of, each inside that organization's scope, and never to any other organization (plan T5).
    /// Maria is the Owner of Cantina da Nonna and a member of Trattoria do Sol;
    /// João is the Owner of Pizzaria Bella.
    /// </summary>
    public class PasswordAuditRecorderTests
    {
        private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
        private static readonly Guid JoaoId = Guid.Parse("22222222-bbbb-4bbb-8bbb-00000000000b");
        private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
        private static readonly Guid TrattoriaId = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");
        private static readonly Guid PizzariaId = Guid.Parse("c3c3c3c3-0000-4000-8000-000000000003");

        private readonly TenantScope _tenantScope = new();
        private readonly IAuditLog _auditLog = Substitute.For<IAuditLog>();
        private readonly List<(Guid? OrganizationId, AuditEntryDto Entry)> _recorded = new();

        public PasswordAuditRecorderTests()
        {
            // Remember which organization scope was open when each entry was recorded.
            _auditLog.When(log => log.RecordAsync(Arg.Any<AuditEntryDto>(), Arg.Any<CancellationToken>()))
                .Do(call => _recorded.Add((_tenantScope.OrganizationId, call.Arg<AuditEntryDto>())));
        }

        private static OrganizationMember Member(Guid userId, Guid organizationId, bool isOwner) => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizationId = organizationId,
            IsOwner = isOwner,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        private static async Task<IdentityTestDatabase> CreateDatabaseAsync(params OrganizationMember[] members)
        {
            var database = new IdentityTestDatabase();
            using var context = database.CreateContext();
            context.Users.AddRange(
                new User { Id = MariaId, Email = "maria@cantinadanonna.com.br", PasswordHash = "hash", FirstName = "Maria", LastName = "Souza", IsVerified = true },
                new User { Id = JoaoId, Email = "joao@pizzariabella.com.br", PasswordHash = "hash", FirstName = "João", LastName = "Lima", IsVerified = true });
            context.Set<OrganizationMember>().AddRange(members);
            await context.SaveChangesAsync();
            return database;
        }

        private async Task RecordAsync(IdentityTestDatabase database, Guid userId, string action)
        {
            using var context = database.CreateContext();
            await new PasswordAuditRecorder(context, _auditLog, _tenantScope).RecordAsync(userId, action);
        }

        // Scenario 12
        [Fact]
        [Trait("Rule", "P6")]
        public async Task RecordAsync_UserWithoutOrganization_RecordsNothing()
        {
            // Arrange
            using var database = await CreateDatabaseAsync(Member(JoaoId, PizzariaId, isOwner: true));

            // Act
            await RecordAsync(database, MariaId, AuditActions.PasswordReset);

            // Assert
            await _auditLog.DidNotReceive().RecordAsync(Arg.Any<AuditEntryDto>(), Arg.Any<CancellationToken>());
        }

        // Scenario 11
        [Fact]
        [Trait("Rule", "P6")]
        public async Task RecordAsync_AnotherUserInAnotherOrganization_NoEntryInThatOrganization()
        {
            // Arrange
            using var database = await CreateDatabaseAsync(
                Member(MariaId, CantinaId, isOwner: true),
                Member(JoaoId, PizzariaId, isOwner: true));

            // Act
            await RecordAsync(database, MariaId, AuditActions.PasswordChanged);

            // Assert
            _recorded.Select(r => r.OrganizationId).ShouldBe(new Guid?[] { CantinaId }, customMessage: "only the organization the user is a member of gets an audit entry, never another user's organization");
        }

        // Scenario 10
        [Theory]
        [InlineData(AuditActions.PasswordReset)]
        [InlineData(AuditActions.PasswordChanged)]
        [Trait("Rule", "P6")]
        public async Task RecordAsync_UserInTwoOrganizations_OneEntryPerOrganizationWithoutValues(string action)
        {
            // Arrange
            using var database = await CreateDatabaseAsync(
                Member(MariaId, CantinaId, isOwner: true),
                Member(MariaId, TrattoriaId, isOwner: false));

            // Act
            await RecordAsync(database, MariaId, action);

            // Assert
            _recorded.Select(r => r.OrganizationId).ShouldBe(new Guid?[] { CantinaId, TrattoriaId }, ignoreOrder: true, customMessage: "the user gets exactly one audit entry in each organization they are a member of");
            _recorded.ShouldAllBe(r => r.Entry.Action == action && r.Entry.EntityType == "User" && r.Entry.EntityId == MariaId, "every entry names the action and the user whose password changed");
            _recorded.ShouldAllBe(r => r.Entry.LocationId == null && r.Entry.Before == null && r.Entry.After == null && r.Entry.Reason == null, "the audit entry never carries password data or any before and after values");
        }
    }
}
