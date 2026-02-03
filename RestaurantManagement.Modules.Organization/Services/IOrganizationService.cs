using RestaurantManagement.Shared;
using System;
using System.Threading.Tasks;
using RestaurantManagement.Modules.Organization.Models;

namespace RestaurantManagement.Modules.Organization.Services
{
    public interface IOrganizationService
    {
        /// <summary>
        /// Creates a new organization and attaches it to a user (Owner role).
        /// </summary>
        /// <param name="ownerUserId">The user that will own the organization.</param>
        /// <param name="request">The organization details.</param>
        /// <returns>A response with the created organization.</returns>
        Task<OrganizationResponse> CreateOrganizationAsync(Guid ownerUserId, CreateOrganizationRequest request);
        Task<OrganizationResponse> EditOrganizationAsync(Guid organizationId, EditOrganizationRequest request);
        Task<List<OrganizationResponse>> GetOrganizationsAsync(Guid userId);
    }
}