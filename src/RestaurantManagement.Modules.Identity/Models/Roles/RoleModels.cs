namespace RestaurantManagement.Modules.Identity.Models.Roles
{
    /// <summary>Levels maps a module name to a level name (None, Read, Manage); a missing module means None.</summary>
    public class CreateRoleRequest
    {
        public string Name { get; set; } = string.Empty;

        public Dictionary<string, string> Levels { get; set; } = new();
    }

    public class UpdateRoleLevelsRequest
    {
        public Dictionary<string, string> Levels { get; set; } = new();
    }

    public class RenameRoleRequest
    {
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>A role with its default level in every module (None included), keyed by module name.</summary>
    public class RoleResponse
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public Dictionary<string, string> Levels { get; set; } = new();
    }
}
