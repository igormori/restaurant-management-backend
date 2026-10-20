using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Services.Identity;

namespace RestaurantManagement.Modules.Identity.Services
{
    public class MemberAccessLookup : IMemberAccessLookup
    {
        private readonly IdentityDbContext _db;

        public MemberAccessLookup(IdentityDbContext db)
        {
            _db = db;
        }

        public async Task<bool> UserExistsAsync(Guid userId)
        {
            return await _db.Users.AnyAsync(u => u.Id == userId);
        }

        public async Task<List<Guid>> GetOrganizationIdsForUserAsync(Guid userId)
        {
            return await _db.OrganizationMembers
                .Where(m => m.UserId == userId)
                .Select(m => m.OrganizationId)
                .ToListAsync();
        }

        public async Task<AccessGridDto> GetAccessGridAsync(Guid userId, Guid organizationId)
        {
            var membership = await _db.OrganizationMembers
                .FirstOrDefaultAsync(m => m.UserId == userId && m.OrganizationId == organizationId);
            if (membership is null)
                return new AccessGridDto();

            var cells = await _db.MemberLocationLevels
                .Where(l => l.UserId == userId && l.OrganizationId == organizationId)
                .ToListAsync();

            return new AccessGridDto
            {
                IsOwner = membership.IsOwner,
                Cells = cells.Select(c => new AccessCellDto { LocationId = c.LocationId, Module = c.Module, Level = c.Level }).ToList()
            };
        }
    }
}
