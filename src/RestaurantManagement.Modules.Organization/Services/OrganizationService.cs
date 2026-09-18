using RestaurantManagement.Shared;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Organization.Data;
using RestaurantManagement.Modules.Organization.Entities;
using RestaurantManagement.Modules.Organization.Models;
using RestaurantManagement.Shared.Services.Identity;
using RestaurantManagement.Shared.Utils.Exceptions;

namespace RestaurantManagement.Modules.Organization.Services
{
    public class OrganizationService : IOrganizationService
    {
        private readonly OrganizationDbContext _orgDb;
        private readonly IUserRoleLookup _userRoleLookup;
        private readonly IUserRoleAssigner _userRoleAssigner;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public OrganizationService(
            OrganizationDbContext orgDb,
            IUserRoleLookup userRoleLookup,
            IUserRoleAssigner userRoleAssigner,
            IStringLocalizer<SharedResource> localizer)
        {
            _orgDb = orgDb;
            _userRoleLookup = userRoleLookup;
            _userRoleAssigner = userRoleAssigner;
            _localizer = localizer;
        }

        public async Task<OrganizationResponse> CreateOrganizationAsync(Guid ownerUserId, CreateOrganizationRequest request)
        {
            var userExists = await _userRoleLookup.UserExistsAsync(ownerUserId);
            if (!userExists)
                throw new InvalidOperationException(_localizer["UserNotFound"].Value);

            // Get user's organization IDs from Identity module
            var userOrgIds = await _userRoleLookup.GetOrganizationIdsForUserAsync(ownerUserId);

            // Get organizations with settings from Organization module
            var userOrgs = await _orgDb.Organizations
                .Where(o => userOrgIds.Contains(o.Id))
                .Include(o => o.Settings)
                .ToListAsync();

            // 🧩 Check if any of the user's organizations are under trial
            bool hasTrialOrg = userOrgs.Any(o =>
                o.Settings != null &&
                o.Settings.PlanType == "TRIAL");

            if (hasTrialOrg)
            {
                throw new BusinessException(_localizer["TrialOrganizationsCannotCreateNew"].Value, 400);
            }

            // 🧪 SENTRY TEST: Trigger an exception for testing
            if (request.Name?.ToUpper() == "TEST_SENTRY")
            {
                throw new Exception("🧪 Sentry Test Exception: This is intentional to test error tracking!");
            }

            using var tx = await _orgDb.Database.BeginTransactionAsync();

            // 1. Create Organization
            var org = new Entities.Organization
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Description = request.Description,
                LogoUrl = request.LogoUrl,
                PrimaryColor = request.PrimaryColor,
                SecondaryColor = request.SecondaryColor,
                AccentColor = request.AccentColor,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _orgDb.Organizations.Add(org);

            // 2. Create Trial Settings
            var settings = new OrganizationSettings
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                PlanType = "TRIAL",
                MaxLocations = 1,
                TrialEndDate = DateTime.UtcNow.AddDays(30),
                IsTrialActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _orgDb.OrganizationSettings.Add(settings);

            try
            {
                // Save Organization and Settings first
                await _orgDb.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                var innerMessage = ex.InnerException?.Message ?? ex.Message;
                throw new Exception($"Failed to create organization: {innerMessage}", ex);
            }

            // 3. Now assign the owner role (after Organization is committed to DB)
            try
            {
                await _userRoleAssigner.AssignRoleAsync(ownerUserId, org.Id, Roles.Owner);
            }
            catch (Exception ex)
            {
                // If UserRole creation fails, we should ideally rollback the organization
                // but since we already committed, we'll just throw the error
                var innerMessage = ex.InnerException?.Message ?? ex.Message;
                throw new Exception($"Organization created but failed to assign owner role: {innerMessage}", ex);
            }

            // 5. Return sanitized response
            return new OrganizationResponse
            {
                Id = org.Id,
                Name = org.Name,
                Description = org.Description,
                LogoUrl = org.LogoUrl,
                PrimaryColor = org.PrimaryColor,
                SecondaryColor = org.SecondaryColor,
                AccentColor = org.AccentColor,
                PlanType = settings.PlanType,
                MaxLocations = settings.MaxLocations,
                TrialEndDate = settings.TrialEndDate,
                IsTrialActive = settings.IsTrialActive
            };
        }
        
        public async Task<OrganizationResponse> EditOrganizationAsync(Guid organizationId, EditOrganizationRequest request)
        {

            // 1. check if organiztion exists and get it
            var organization = await _orgDb.Organizations.FirstOrDefaultAsync(org => org.Id == organizationId);
            if (organization == null)
                throw new InvalidOperationException(_localizer["OrganizationNotFound"].Value);

            // 2. Get the organiztion settings
            var organizationSettings = await _orgDb.OrganizationSettings.FirstOrDefaultAsync(orgS => orgS.OrganizationId == organizationId);
            if (organizationSettings == null)
                throw new InvalidOperationException(_localizer["OrganizationSettingsNotFound"].Value);

            // 2. Edit Organization Information
            organization.Name = request.Name;
            organization.Description = request.Description;
            organization.LogoUrl = request.LogoUrl;
            organization.PrimaryColor = request.PrimaryColor;
            organization.SecondaryColor = request.SecondaryColor;
            organization.AccentColor = request.AccentColor;
            organization.UpdatedAt = DateTime.UtcNow;

            await _orgDb.SaveChangesAsync();

            // 4. Return sanitized response
            return new OrganizationResponse
            {
                Id = organization.Id,
                Name = organization.Name,
                Description = organization.Description,
                LogoUrl = organization.LogoUrl,
                PrimaryColor = organization.PrimaryColor,
                SecondaryColor = organization.SecondaryColor,
                AccentColor = organization.AccentColor,
                PlanType = organizationSettings.PlanType,
                MaxLocations = organizationSettings.MaxLocations,
                TrialEndDate = organizationSettings.TrialEndDate,
                IsTrialActive = organizationSettings.IsTrialActive
            };
        }
        public async Task<List<OrganizationResponse>> GetOrganizationsAsync(Guid userId)
        {
            var userExists = await _userRoleLookup.UserExistsAsync(userId);
            if (!userExists)
                throw new InvalidOperationException(_localizer["UserNotFound"].Value);

            // Get organization IDs from Identity module
            var orgIds = await _userRoleLookup.GetOrganizationIdsForUserAsync(userId);

            // Get organizations with settings from Organization module
            var organizations = await _orgDb.Organizations
                .Where(o => orgIds.Contains(o.Id))
                .Include(o => o.Settings)
                .Select(o => new OrganizationResponse
                {
                    Id = o.Id,
                    Name = o.Name,
                    Description = o.Description,
                    LogoUrl = o.LogoUrl,
                    PrimaryColor = o.PrimaryColor,
                    SecondaryColor = o.SecondaryColor,
                    AccentColor = o.AccentColor,
                    PlanType = o.Settings != null ? o.Settings.PlanType : "UNKNOWN",
                    MaxLocations = o.Settings != null ? o.Settings.MaxLocations : 0,
                    TrialEndDate = o.Settings != null ? o.Settings.TrialEndDate : (DateTime?)null,
                    IsTrialActive = o.Settings != null ? o.Settings.IsTrialActive : false
                })
                .ToListAsync();

            return organizations;
        }
    }
}