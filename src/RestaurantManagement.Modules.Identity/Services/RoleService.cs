using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Services.AuditLog;

namespace RestaurantManagement.Modules.Identity.Services
{
    public class RoleService : IRoleService
    {
        public RoleService(
            IdentityDbContext db,
            ITenantContext tenantContext,
            IRoleManagementRules rules,
            IAuditLog auditLog,
            IStringLocalizer<SharedResource> localizer,
            ILogger<RoleService> logger)
        {
        }

        public Task<List<RoleResponse>> ListAsync() => throw new NotImplementedException();

        public Task<RoleResponse> GetAsync(Guid roleId) => throw new NotImplementedException();

        public Task<RoleResponse> CreateAsync(CreateRoleRequest request) => throw new NotImplementedException();

        public Task<RoleResponse> UpdateLevelsAsync(Guid roleId, UpdateRoleLevelsRequest request) => throw new NotImplementedException();

        public Task<RoleResponse> RenameAsync(Guid roleId, RenameRoleRequest request) => throw new NotImplementedException();
    }
}
