using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Shared.Auth;

namespace RestaurantManagement.Modules.Identity.Services
{
    /// <summary>
    /// Sets and reads a member's access in the token organization. Setting runs the delegation
    /// rules (who may grant what) before any write.
    /// </summary>
    public interface IMemberAccessService
    {
        /// <summary>
        /// Sets a member's access at one location: the chosen roles are merged (higher level per
        /// cell wins), the changed levels are applied on top, and the final levels and role labels
        /// are stored. Refused by the delegation rules (403): nothing stored. A non-member, or a role or location of
        /// another organization: 404 and nothing stored. One audit entry per change. Returns the member's access at that location. Returns the member's access at that location.
        /// </summary>
        Task<MemberLocationAccessResponse> SetAccessAsync(
            Guid userId,
            Guid locationId,
            IReadOnlyCollection<string> roleNames,
            IReadOnlyDictionary<AccessModule, AccessLevel> changedLevels);

        /// <summary>The member's current role names and levels, per location.</summary>
        Task<List<MemberLocationAccessResponse>> GetAccessAsync(Guid userId);
    }
}
