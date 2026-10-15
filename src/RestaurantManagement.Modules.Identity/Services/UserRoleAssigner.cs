using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Shared.Services.Identity;

namespace RestaurantManagement.Modules.Identity.Services
{
    public class UserRoleAssigner : IUserRoleAssigner
    {
        private readonly IdentityDbContext _db;

        public UserRoleAssigner(IdentityDbContext db)
        {
            _db = db;
        }

        public async Task AssignRoleAsync(Guid userId, Guid organizationId, string role)
        {
            var userRole = new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OrganizationId = organizationId,
                LocationId = null,
                Role = role,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.UserRoles.Add(userRole);
            await _db.SaveChangesAsync();
        }

        public async Task RevokeRoleAsync(Guid userId, Guid organizationId, string role)
        {
            var userRole = await _db.UserRoles.FirstOrDefaultAsync(ur =>
                ur.UserId == userId && ur.OrganizationId == organizationId && ur.Role == role);
            if (userRole is null)
                return;

            _db.UserRoles.Remove(userRole);
            await _db.SaveChangesAsync();
        }
    }
}
