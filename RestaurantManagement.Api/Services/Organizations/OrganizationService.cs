using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Api.Data;
using RestaurantManagement.Api.Entities.Organizations;
using RestaurantManagement.Api.Entities.Locations;
using RestaurantManagement.Api.Entities.Users;
using RestaurantManagement.Api.Models.Organizations;

namespace RestaurantManagement.Api.Services.Organizations
{
    public class OrganizationService : IOrganizationService
    {
        private readonly RestaurantDbContext _db;

        public OrganizationService(RestaurantDbContext db)
        {
            _db = db;
        }

        public async Task<OrganizationResponse> CreateForUserAsync(Guid ownerUserId, CreateOrganizationRequest request)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == ownerUserId);
            if (user == null)
                throw new InvalidOperationException("User not found.");

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
    }
}