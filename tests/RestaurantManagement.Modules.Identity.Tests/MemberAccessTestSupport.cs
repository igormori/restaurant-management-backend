using Microsoft.Extensions.Logging;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Entities.Members;
using RestaurantManagement.Modules.Identity.Entities.Roles;
using RestaurantManagement.Modules.Identity.Entities.Users;
using RestaurantManagement.Modules.Identity.Services.Members;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Data.Tenancy;
using RestaurantManagement.Shared.Services.AuditLog;
using RestaurantManagement.Shared.Services.Organization;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// Shared setup for the access grid tests: Cantina da Nonna (Batel and Mercado Municipal) and
    /// Trattoria do Sol, with their stored roles, an Owner (Ana), a member of Cantina (Maria), a member of
    /// Trattoria (Paolo) and a user who belongs to no organization (Rita).
    /// </summary>
    public static class MemberAccessTestSupport
    {
        public static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
        public static readonly Guid TrattoriaId = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");
        public static readonly Guid BatelId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000001");
        public static readonly Guid MercadoId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000002");
        public static readonly Guid TrattoriaCentroId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000003");
        public static readonly Guid AnaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
        public static readonly Guid MariaId = Guid.Parse("22222222-bbbb-4bbb-8bbb-00000000000b");
        public static readonly Guid PaoloId = Guid.Parse("33333333-cccc-4ccc-8ccc-00000000000c");
        public static readonly Guid RitaId = Guid.Parse("44444444-dddd-4ddd-8ddd-00000000000d");

        private static readonly DateTime CreationTime = new(2026, 10, 20, 18, 30, 0, DateTimeKind.Utc);

        /// <summary>The spec table "Default levels per role" (cells not listed are None).</summary>
        public static readonly IReadOnlyDictionary<string, (AccessModule Module, AccessLevel Level)[]> DefaultLevels =
            new Dictionary<string, (AccessModule, AccessLevel)[]>
            {
                ["Admin"] = new[]
                {
                    (AccessModule.Locations, AccessLevel.Manage), (AccessModule.Menus, AccessLevel.Manage), (AccessModule.Cashier, AccessLevel.Manage),
                    (AccessModule.Orders, AccessLevel.Manage), (AccessModule.Kitchen, AccessLevel.Manage), (AccessModule.Reports, AccessLevel.Manage),
                    (AccessModule.Members, AccessLevel.Manage)
                },
                ["Manager"] = new[]
                {
                    (AccessModule.Locations, AccessLevel.Read), (AccessModule.Menus, AccessLevel.Manage), (AccessModule.Orders, AccessLevel.Read),
                    (AccessModule.Kitchen, AccessLevel.Read), (AccessModule.Reports, AccessLevel.Manage)
                },
                ["Cashier"] = new[]
                {
                    (AccessModule.Locations, AccessLevel.Read), (AccessModule.Menus, AccessLevel.Read), (AccessModule.Cashier, AccessLevel.Manage),
                    (AccessModule.Orders, AccessLevel.Read)
                },
                ["Waiter"] = new[]
                {
                    (AccessModule.Locations, AccessLevel.Read), (AccessModule.Menus, AccessLevel.Read), (AccessModule.Orders, AccessLevel.Manage)
                },
                ["Kitchen"] = new[]
                {
                    (AccessModule.Locations, AccessLevel.Read), (AccessModule.Menus, AccessLevel.Read), (AccessModule.Orders, AccessLevel.Read),
                    (AccessModule.Kitchen, AccessLevel.Manage)
                }
            };

        public static OrganizationRole Role(Guid organizationId, string name) => new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = name,
            CreatedAt = CreationTime,
            UpdatedAt = CreationTime
        };

        public static IEnumerable<OrganizationRoleLevel> LevelsOf(OrganizationRole role) =>
            DefaultLevels[role.Name].Select(l => new OrganizationRoleLevel
            {
                Id = Guid.NewGuid(),
                OrganizationId = role.OrganizationId,
                RoleId = role.Id,
                Module = l.Module,
                Level = l.Level,
                CreatedAt = CreationTime,
                UpdatedAt = CreationTime
            });

        /// <summary>
        /// Stores the users and memberships above, and in each organization the roles named (with the
        /// spec's default levels). No access cells are stored: every member starts with nothing.
        /// </summary>
        public static async Task<IdentityTestDatabase> CreateDatabaseAsync(params string[] roleNames)
        {
            var database = new IdentityTestDatabase();
            using var context = database.CreateContext();
            context.Users.AddRange(User(AnaId, "ana@cantinadanonna.com.br", "Ana"), User(MariaId, "maria@cantinadanonna.com.br", "Maria"),
                User(PaoloId, "paolo@trattoriadosol.com.br", "Paolo"), User(RitaId, "rita@exemplo.com.br", "Rita"));
            context.Set<OrganizationMember>().AddRange(
                TestMembers.Owner(AnaId, CantinaId), TestMembers.Member(MariaId, CantinaId), TestMembers.Member(PaoloId, TrattoriaId));
            foreach (var organizationId in new[] { CantinaId, TrattoriaId })
                foreach (var name in roleNames)
                {
                    var role = Role(organizationId, name);
                    context.Set<OrganizationRole>().Add(role);
                    context.Set<OrganizationRoleLevel>().AddRange(LevelsOf(role));
                }

            await context.SaveChangesAsync();
            return database;
        }

        private static User User(Guid id, string email, string firstName) => new()
        {
            Id = id,
            Email = email,
            PasswordHash = "hash",
            FirstName = firstName,
            LastName = "Souza",
            IsVerified = true
        };

        public static IOrganizationLookup Locations()
        {
            var lookup = Substitute.For<IOrganizationLookup>();
            lookup.GetLocationAsync(BatelId).Returns(new LocationSummaryDto { Id = BatelId, OrganizationId = CantinaId });
            lookup.GetLocationAsync(MercadoId).Returns(new LocationSummaryDto { Id = MercadoId, OrganizationId = CantinaId });
            lookup.GetLocationAsync(TrattoriaCentroId).Returns(new LocationSummaryDto { Id = TrattoriaCentroId, OrganizationId = TrattoriaId });
            return lookup;
        }

        public static ITenantContext Tenant(Guid organizationId)
        {
            var tenant = Substitute.For<ITenantContext>();
            tenant.OrganizationId.Returns(organizationId);
            return tenant;
        }

        public static MemberAccessService CreateService(
            IdentityTestDatabase database, IAuditLog? auditLog = null, ILogger<MemberAccessService>? logger = null)
            => new(database.CreateContext(), Locations(), Tenant(CantinaId), auditLog ?? Substitute.For<IAuditLog>(),
                TestLocalizer.Create(), logger ?? Substitute.For<ILogger<MemberAccessService>>(),
                Substitute.For<IMemberDelegationRules>(), Substitute.For<IMemberGridBuilder>());

        /// <summary>Every stored cell of a member in an organization, read through a fresh context.</summary>
        public static List<(Guid LocationId, AccessModule Module, AccessLevel Level)> StoredCells(IdentityTestDatabase database, Guid userId, Guid organizationId)
        {
            using var context = database.CreateContext();
            return context.Set<MemberLocationLevel>()
                .Where(c => c.UserId == userId && c.OrganizationId == organizationId)
                .AsEnumerable()
                .Select(c => (c.LocationId, c.Module, c.Level))
                .ToList();
        }
    }
}
