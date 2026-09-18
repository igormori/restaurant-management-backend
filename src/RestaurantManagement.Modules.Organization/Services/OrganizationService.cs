using RestaurantManagement.Shared;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
        private readonly ILogger<OrganizationService> _logger;

        public OrganizationService(
            OrganizationDbContext orgDb,
            IUserRoleLookup userRoleLookup,
            IUserRoleAssigner userRoleAssigner,
            IStringLocalizer<SharedResource> localizer,
            ILogger<OrganizationService> logger)
        {
            _orgDb = orgDb;
            _userRoleLookup = userRoleLookup;
            _userRoleAssigner = userRoleAssigner;
            _localizer = localizer;
            _logger = logger;
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
                // Save Organization and Settings, but do not commit yet: the organization
                // must not become visible to any query until the creating user is
                // confirmed as Owner.
                await _orgDb.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                await RollbackAsync(tx, ex);
                var innerMessage = ex.InnerException?.Message ?? ex.Message;
                throw new Exception($"Failed to create organization: {innerMessage}", ex);
            }

            // 3. Assign the owner role while still inside the transaction, so a failure
            // here rolls back the organization instead of leaving it orphaned.
            try
            {
                await _userRoleAssigner.AssignRoleAsync(ownerUserId, org.Id, Roles.Owner);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to assign Owner role to user {UserId} for organization {OrganizationId}; rolling back organization creation.", ownerUserId, org.Id);
                await RollbackAsync(tx, ex);
                throw;
            }

            // 4. Commit only after the Owner role is confirmed.
            try
            {
                await tx.CommitAsync();
            }
            catch (Exception ex)
            {
                // The owner role was already written to Identity's database, but the
                // organization itself never committed: this UserRole now points at a
                // non-existent organization and needs manual cleanup.
                _logger.LogError(ex, "Failed to commit organization {OrganizationId} after owner role was assigned to user {UserId}; a dangling UserRole for a non-existent organization may exist.", org.Id, ownerUserId);
                throw;
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

        private async Task RollbackAsync(IDbContextTransaction tx, Exception causeException)
        {
            try
            {
                await tx.RollbackAsync();
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(rollbackEx, "Failed to roll back organization creation after error: {CauseMessage}", causeException.Message);
            }
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