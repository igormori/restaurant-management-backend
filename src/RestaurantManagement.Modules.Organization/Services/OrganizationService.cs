using RestaurantManagement.Shared;
using System;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RestaurantManagement.Modules.Organization.Data;
using RestaurantManagement.Modules.Organization.Entities;
using RestaurantManagement.Modules.Organization.Models;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Services.AuditLog;
using RestaurantManagement.Shared.Services.Identity;
using RestaurantManagement.Shared.Utils.Exceptions;

namespace RestaurantManagement.Modules.Organization.Services
{
    public class OrganizationService : IOrganizationService
    {
        private readonly OrganizationDbContext _orgDb;
        private readonly IUserRoleLookup _userRoleLookup;
        private readonly IUserRoleAssigner _userRoleAssigner;
        private readonly ITenantScope _tenantScope;
        private readonly ITenantContext _tenantContext;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly ILogger<OrganizationService> _logger;
        private readonly IAuditLog _auditLog;

        public OrganizationService(
            OrganizationDbContext orgDb,
            IUserRoleLookup userRoleLookup,
            IUserRoleAssigner userRoleAssigner,
            ITenantScope tenantScope,
            ITenantContext tenantContext,
            IStringLocalizer<SharedResource> localizer,
            ILogger<OrganizationService> logger,
            IAuditLog auditLog)
        {
            _orgDb = orgDb;
            _userRoleLookup = userRoleLookup;
            _userRoleAssigner = userRoleAssigner;
            _tenantScope = tenantScope;
            _tenantContext = tenantContext;
            _localizer = localizer;
            _logger = logger;
            _auditLog = auditLog;
        }

        public async Task<OrganizationResponse> CreateOrganizationAsync(Guid ownerUserId, CreateOrganizationRequest request)
        {
            var userExists = await _userRoleLookup.UserExistsAsync(ownerUserId);
            if (!userExists)
                throw new BusinessException(_localizer["UserNotFound"].Value, 401);

            // Get user's organization IDs from Identity module
            var userOrgIds = await _userRoleLookup.GetOrganizationIdsForUserAsync(ownerUserId);

            // Get organizations with settings from Organization module
            var userOrgs = await _orgDb.Organizations
                .Where(o => userOrgIds.Contains(o.Id))
                .IgnoreQueryFilters() // Filter-bypass rule: the user's own memberships may have no token organization yet
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
                Currency = Enum.Parse<Currency>(request.Currency!),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _orgDb.Organizations.Add(org);

            // The new organization does not exist in the user's token yet, so the audit
            // interceptor and the role assignment must see it through the scope.
            using var tenantScope = _tenantScope.Begin(org.Id);

            // 2. Create Trial Settings
            var settings = new OrganizationSettings
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                PlanType = "TRIAL",
                MaxLocations = 1,
                TrialEndDate = DateTime.UtcNow.AddDays(30),
                IsTrialActive = true,
                ServiceChargeRate = 0,
                DiscountApprovalLimit = 0,
                BusinessDayCutoff = new TimeOnly(4, 0),
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
                // non-existent organization. Compensate by revoking it as a best-effort
                // cleanup, without masking the original commit failure.
                _logger.LogError(ex, "Failed to commit organization {OrganizationId} after owner role was assigned to user {UserId}; a dangling UserRole for a non-existent organization may exist.", org.Id, ownerUserId);
                await RevokeRoleBestEffortAsync(ownerUserId, org.Id, ex);
                throw;
            }

            // 5. Return sanitized response
            return ToResponse(org, settings);
        }

        private async Task RevokeRoleBestEffortAsync(Guid ownerUserId, Guid organizationId, Exception causeException)
        {
            try
            {
                await _userRoleAssigner.RevokeRoleAsync(ownerUserId, organizationId, Roles.Owner);
            }
            catch (Exception revokeEx)
            {
                _logger.LogError(revokeEx, "Failed to revoke Owner role from user {UserId} for organization {OrganizationId} after commit failure: {CauseMessage}", ownerUserId, organizationId, causeException.Message);
            }
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

        public async Task<OrganizationResponse> EditOrganizationAsync(Guid userId, EditOrganizationRequest request)
        {
            var organizationId = _tenantContext.OrganizationId
                ?? throw new BusinessException(_localizer["OrganizationRequired"].Value, 403);

            // 1. check if organiztion exists and get it
            var organization = await _orgDb.Organizations.FirstOrDefaultAsync(org => org.Id == organizationId);
            if (organization == null)
                throw new BusinessException(_localizer["OrganizationNotFound"].Value, 404);

            // 2. Get the organiztion settings
            var organizationSettings = await _orgDb.OrganizationSettings.FirstOrDefaultAsync(orgS => orgS.OrganizationId == organizationId);
            if (organizationSettings == null)
                throw new BusinessException(_localizer["OrganizationSettingsNotFound"].Value, 404);

            // 3. Only Owner or Admin of this organization may edit it
            var role = await _userRoleLookup.GetRoleAsync(userId, organizationId);
            if (role != Roles.Owner && role != Roles.Admin)
                throw new BusinessException(_localizer["UserNotAdminOrOwner"].Value, 403);

            var before = (organizationSettings.ServiceChargeRate, organizationSettings.DiscountApprovalLimit, organizationSettings.BusinessDayCutoff);

            // 2. Edit Organization Information
            organization.Name = request.Name;
            organization.Description = request.Description;
            organization.LogoUrl = request.LogoUrl;
            organization.PrimaryColor = request.PrimaryColor;
            organization.SecondaryColor = request.SecondaryColor;
            organization.AccentColor = request.AccentColor;
            organization.UpdatedAt = DateTime.UtcNow;
            organizationSettings.ServiceChargeRate = request.ServiceChargeRate!.Value;
            organizationSettings.DiscountApprovalLimit = request.DiscountApprovalLimit!.Value;
            organizationSettings.BusinessDayCutoff = TimeOnly.ParseExact(request.BusinessDayCutoff!, "HH:mm", CultureInfo.InvariantCulture);

            await _orgDb.SaveChangesAsync();

            await RecordSettingsChangeAsync(organizationSettings, before);

            // 4. Return sanitized response
            return ToResponse(organization, organizationSettings);
        }

        // The edit is already saved; a failed audit write must not undo it or fail the request.
        private async Task RecordSettingsChangeAsync(OrganizationSettings settings, (decimal Rate, decimal Limit, TimeOnly Cutoff) before)
        {
            if (before == (settings.ServiceChargeRate, settings.DiscountApprovalLimit, settings.BusinessDayCutoff))
                return;

            try
            {
                await _auditLog.RecordAsync(new AuditEntryDto
                {
                    Action = AuditActions.SettingsChanged,
                    EntityType = "OrganizationSettings",
                    EntityId = settings.Id,
                    Before = ToSettingsJson(before.Rate, before.Limit, before.Cutoff),
                    After = ToSettingsJson(settings.ServiceChargeRate, settings.DiscountApprovalLimit, settings.BusinessDayCutoff)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record the settings change for organization {OrganizationId}.", settings.OrganizationId);
            }
        }

        private static string ToSettingsJson(decimal rate, decimal limit, TimeOnly cutoff) => JsonSerializer.Serialize(new
        {
            serviceChargeRate = rate,
            discountApprovalLimit = limit,
            businessDayCutoff = cutoff.ToString("HH:mm", CultureInfo.InvariantCulture)
        });

        public async Task<List<OrganizationResponse>> GetOrganizationsAsync(Guid userId)
        {
            var userExists = await _userRoleLookup.UserExistsAsync(userId);
            if (!userExists)
                throw new BusinessException(_localizer["UserNotFound"].Value, 401);

            // Get organization IDs from Identity module
            var orgIds = await _userRoleLookup.GetOrganizationIdsForUserAsync(userId);

            // Get organizations with settings from Organization module
            var organizations = await _orgDb.Organizations
                .Where(o => orgIds.Contains(o.Id))
                .IgnoreQueryFilters() // Filter-bypass rule: lists plan details for every one of the user's memberships
                .Include(o => o.Settings)
                .ToListAsync();

            return organizations.Select(o => ToResponse(o, o.Settings)).ToList();
        }

        private static OrganizationResponse ToResponse(Entities.Organization org, OrganizationSettings? settings) => new()
        {
            Id = org.Id,
            Name = org.Name,
            Description = org.Description,
            LogoUrl = org.LogoUrl,
            PrimaryColor = org.PrimaryColor,
            SecondaryColor = org.SecondaryColor,
            AccentColor = org.AccentColor,
            Currency = org.Currency,
            PlanType = settings?.PlanType ?? "UNKNOWN",
            MaxLocations = settings?.MaxLocations ?? 0,
            TrialEndDate = settings?.TrialEndDate,
            IsTrialActive = settings?.IsTrialActive ?? false,
            ServiceChargeRate = settings?.ServiceChargeRate ?? 0,
            DiscountApprovalLimit = settings?.DiscountApprovalLimit ?? 0,
            BusinessDayCutoff = (settings?.BusinessDayCutoff ?? default).ToString("HH:mm", CultureInfo.InvariantCulture)
        };
    }
}
