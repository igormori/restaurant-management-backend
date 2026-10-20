namespace RestaurantManagement.Shared.Services.Identity
{
    /// <summary>
    /// Sets up and undoes the access side of a new organization, so Organization never touches
    /// Identity's tables.
    /// </summary>
    public interface IOrganizationAccessSetup
    {
        /// <summary>Adds the Owner membership and the default roles of a new organization.</summary>
        Task AddOwnerWithDefaultRolesAsync(Guid ownerUserId, Guid organizationId);

        /// <summary>Removes the Owner membership and the roles of that organization only (used when creation fails).</summary>
        Task RemoveOwnerAndRolesAsync(Guid ownerUserId, Guid organizationId);
    }
}
