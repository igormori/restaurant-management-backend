using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Api.Data;
using RestaurantManagement.Api.Entities.Organizations;
using RestaurantManagement.Api.Entities.Locations;
using RestaurantManagement.Api.Entities.Users;
using RestaurantManagement.Api.Models.Organizations;
using RestaurantManagement.Api.Utils.Exceptions;

namespace RestaurantManagement.Api.Services.Organizations
{
    public class OrganizationService : IOrganizationService
    {
        private readonly RestaurantDbContext _db;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public OrganizationService(RestaurantDbContext db, IStringLocalizer<SharedResource> localizer)
        {
            _db = db;
            _localizer = localizer;
        }

        public async Task<OrganizationResponse> CreateOrganizationAsync(Guid ownerUserId, CreateOrganizationRequest request)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == ownerUserId);
            if (user == null)
                throw new InvalidOperationException(_localizer["UserNotFound"].Value);

            var userRoles = await _db.UserRoles
                .Include(r => r.Organization)
                .ThenInclude(o => o.Settings)
                .Where(usr => usr.UserId == user.Id)
                .ToListAsync();

            var userOrgs = await _db.UserRoles
                .Where(r => r.UserId == user.Id)
                .Include(r => r.Organization)
                    .ThenInclude(o => o.Settings)
                .Select(r => r.Organization)
                .ToListAsync();

            // 🧩 Check if any of the user's organizations are under trial
            bool hasTrialOrg = userOrgs.Any(o =>
                o.Settings != null &&
                o.Settings.PlanType == "TRIAL");

            if (hasTrialOrg)
            {
                throw new BusinessException(_localizer["TrialOrganizationsCannotCreateNew"].Value, 400);
            }

            using var tx = await _db.Database.BeginTransactionAsync();

            // 1. Create Organization
            var org = new Organization
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
            _db.Organizations.Add(org);

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
            _db.OrganizationSettings.Add(settings);

            // 3. Create First Location
            var location = new Location
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                Name = request.LocationName,
                Address = request.Address,
                City = request.City,
                State = request.State,
                PostalCode = request.PostalCode,
                Country = request.Country,
                PhoneNumber = request.PhoneNumber,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Locations.Add(location);

            // 4. Assign User as Owner with Write permission
            var ownerRole = new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = ownerUserId,
                OrganizationId = org.Id,
                LocationId = null, // Org-wide
                Role = "Owner"
            };
            _db.UserRoles.Add(ownerRole);

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

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
        
        public async Task<OrganizationResponse> EditOrganizationAsync(Guid organizationId, CreateOrganizationRequest request)
        {

            // 1. check if organiztion exists and get it
            var organization = await _db.Organizations.FirstOrDefaultAsync(org => org.Id == organizationId);
            if (organization == null)
                throw new InvalidOperationException(_localizer["OrganizationNotFound"].Value);

            // 2. Get the organiztion settings
            var organizationSettings = await _db.OrganizationSettings.FirstOrDefaultAsync(orgS => orgS.OrganizationId == organizationId);
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

            await _db.SaveChangesAsync();

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
    }
}