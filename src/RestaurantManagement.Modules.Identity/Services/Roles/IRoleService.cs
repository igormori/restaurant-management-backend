using RestaurantManagement.Modules.Identity.Models;

namespace RestaurantManagement.Modules.Identity.Services.Roles
{
    /// <summary>
    /// Lists, reads, creates, edits and renames the roles of the token organization. Names are trimmed and
    /// refused with 400 (nothing saved) when empty, over 100 characters, "Owner" or already used in the
    /// organization in any letter case; an unknown module or level name is 400. A role of another
    /// organization is 404. Create, edit and rename run the role-management rules before any write and are
    /// audited (role.created, role.levels_changed, role.renamed; none when nothing changed).
    /// </summary>
    public interface IRoleService
    {
        Task<List<RoleResponse>> ListAsync();

        Task<RoleResponse> GetAsync(Guid roleId);

        Task<RoleResponse> CreateAsync(CreateRoleRequest request);

        Task<RoleResponse> UpdateLevelsAsync(Guid roleId, UpdateRoleLevelsRequest request);

        Task<RoleResponse> RenameAsync(Guid roleId, RenameRoleRequest request);
    }
}
