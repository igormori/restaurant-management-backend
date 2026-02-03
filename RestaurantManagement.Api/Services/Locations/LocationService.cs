using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using RestaurantManagement.Api.Data;
using RestaurantManagement.Api.Entities.Locations;
using RestaurantManagement.Api.Models.Locations;
using RestaurantManagement.Api.Utils.Exceptions;

namespace RestaurantManagement.Api.Services.Locations
{
    public class LocationService : ILocationService
    {
        private readonly RestaurantDbContext _db;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public LocationService(RestaurantDbContext db, IStringLocalizer<SharedResource> localizer)
        {
            _db = db;
            _localizer = localizer;
        }

        public async Task<LocationResponse> CreateLocationAsync(Guid userId, Guid organizationId, CreateLocationRequest request)
        {
            // 1. Verify User is Owner of the Organization
            var userRole = await _db.UserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.OrganizationId == organizationId);

            if (userRole == null || userRole.Role != "Owner")
                throw new BusinessException(_localizer["UserNotAdminOrOwner"].Value, 403);

            // 2. Create Location
            var location = new Location
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = request.Name,
                Address = request.Address,
                City = request.City,
                State = request.State,
                PostalCode = request.PostalCode,
                Country = request.Country,
                PhoneNumber = request.PhoneNumber,
                Status = LocationStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Locations.Add(location);
            await _db.SaveChangesAsync();

            return MapToResponse(location);
        }

        public async Task<LocationResponse> EditLocationAsync(Guid userId, Guid locationId, EditLocationRequest request)
        {
            var location = await _db.Locations.FirstOrDefaultAsync(l => l.Id == locationId);
            if (location == null)
                throw new BusinessException(_localizer["LocationNotFound"].Value, 404);

            // Verify User is Owner of the Organization (derived from location)
            var userRole = await _db.UserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.OrganizationId == location.OrganizationId);

            if (userRole == null || userRole.Role != "Owner")
                throw new BusinessException(_localizer["UserNotAdminOrOwner"].Value, 403);

            location.Name = request.Name;
            location.Address = request.Address;
            location.City = request.City;
            location.State = request.State;
            location.PostalCode = request.PostalCode;
            location.Country = request.Country;
            location.PhoneNumber = request.PhoneNumber;
            location.Status = request.Status;
            location.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return MapToResponse(location);
        }

        public async Task DeleteLocationAsync(Guid userId, Guid locationId)
        {
            var location = await _db.Locations.FirstOrDefaultAsync(l => l.Id == locationId);
            if (location == null)
                throw new BusinessException(_localizer["LocationNotFound"].Value, 404);

            // Verify User is Owner
            var userRole = await _db.UserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.OrganizationId == location.OrganizationId);

            if (userRole == null || userRole.Role != "Owner")
                throw new BusinessException(_localizer["UserNotAdminOrOwner"].Value, 403);

            // Soft delete by setting status to Closed (or logic as discussed: status is status)
            // User requested "If status closes ... do not delete". But "Delete" endpoint generally means "I want it gone or inactive".
            // Implementation: Set to Closed as a form of soft delete or just updates status. 
            // Better: Just set status to Closed.
            // Wait, "Delete" usually implies "Gone". But requirement says "If user closes... don't delete data".
            // So "Delete" endpoint creates a "Closed" state.
            
            location.Status = LocationStatus.Closed;
            location.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        public async Task<List<LocationResponse>> GetLocationsByOrganizationAsync(Guid userId, Guid organizationId)
        {
            // Verify User belongs to Organization (any role)
            var userRole = await _db.UserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.OrganizationId == organizationId);

            if (userRole == null)
                throw new BusinessException(_localizer["UserNotMemberOfOrganization"].Value, 403);

            var locations = await _db.Locations
                .Where(l => l.OrganizationId == organizationId)
                .OrderBy(l => l.Name)
                .ToListAsync();

            return locations.Select(MapToResponse).ToList();
        }

        private static LocationResponse MapToResponse(Location location)
        {
            return new LocationResponse
            {
                Id = location.Id,
                OrganizationId = location.OrganizationId,
                Name = location.Name,
                Address = location.Address,
                City = location.City,
                State = location.State,
                PostalCode = location.PostalCode,
                Country = location.Country,
                PhoneNumber = location.PhoneNumber,
                Status = location.Status,
                CreatedAt = location.CreatedAt,
                UpdatedAt = location.UpdatedAt
            };
        }
    }
}
