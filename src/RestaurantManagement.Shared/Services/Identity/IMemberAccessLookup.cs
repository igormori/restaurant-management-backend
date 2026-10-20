using RestaurantManagement.Shared.Auth;

namespace RestaurantManagement.Shared.Services.Identity
{
    /// <summary>
    /// Identity's answer to "what may this user do": read from storage on every call, so a
    /// change applies at once.
    /// </summary>
    public interface IMemberAccessLookup
    {
        Task<bool> UserExistsAsync(Guid userId);

        /// <summary>The organizations the user is a member of.</summary>
        Task<List<Guid>> GetOrganizationIdsForUserAsync(Guid userId);

        /// <summary>
        /// The user's grid in one organization: the Owner flag and only that organization's cells.
        /// A user who is not a member gets no Owner flag and no cells.
        /// </summary>
        Task<AccessGridDto> GetAccessGridAsync(Guid userId, Guid organizationId);
    }
}
