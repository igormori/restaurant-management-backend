using Microsoft.Extensions.Logging;
using NSubstitute;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Identity.Services.Roles;
using RestaurantManagement.Shared.Services.AuditLog;
using static RestaurantManagement.Modules.Identity.Tests.MemberAccessTestSupport;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// Shared setup for the role service tests: Cantina da Nonna and Trattoria do Sol each have the five default
    /// roles with the spec's default levels. The role-management rules are replaced, so these tests prove what the
    /// service stores and audits; the rules themselves are in <c>RoleManagementRulesTests</c>.
    /// </summary>
    public static class RoleServiceTestSupport
    {
        public static readonly string[] DefaultRoles = { "Admin", "Manager", "Cashier", "Waiter", "Kitchen" };

        public static RoleService CreateRoleService(
            IdentityTestDatabase database, IRoleManagementRules? rules = null, IAuditLog? auditLog = null)
            => new(database.CreateContext(), Tenant(CantinaId), Substitute.For<IAccessGuard>(), rules ?? Substitute.For<IRoleManagementRules>(),
                auditLog ?? Substitute.For<IAuditLog>(), TestLocalizer.Create(), Substitute.For<ILogger<RoleService>>());

        public static Guid RoleIdOf(IdentityTestDatabase database, Guid organizationId, string name)
        {
            using var context = database.CreateContext();
            return context.Set<OrganizationRole>().Single(r => r.OrganizationId == organizationId && r.Name == name).Id;
        }

        public static List<string> StoredRoleNames(IdentityTestDatabase database, Guid organizationId)
        {
            using var context = database.CreateContext();
            return context.Set<OrganizationRole>().Where(r => r.OrganizationId == organizationId).Select(r => r.Name).ToList()
                .OrderBy(n => n).ToList();
        }

        /// <summary>A role's stored default levels as "Module:Level", ordered by module (None is never stored).</summary>
        public static string StoredLevels(IdentityTestDatabase database, Guid organizationId, Guid roleId)
        {
            using var context = database.CreateContext();
            var levels = context.Set<OrganizationRoleLevel>().Where(l => l.OrganizationId == organizationId && l.RoleId == roleId).ToList();
            return string.Join(",", levels.OrderBy(l => l.Module).Select(l => $"{l.Module}:{l.Level}"));
        }

        /// <summary>The audit entry the service handed to the audit log, or null when it recorded nothing.</summary>
        public static AuditEntryDto? Recorded(IAuditLog auditLog)
            => auditLog.ReceivedCalls().Select(c => c.GetArguments()[0]).OfType<AuditEntryDto>().SingleOrDefault();
    }
}
