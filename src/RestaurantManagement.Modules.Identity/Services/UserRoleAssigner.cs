using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Services.Identity;

namespace RestaurantManagement.Modules.Identity.Services
{
    // Owner-only stand-in until the grid replaces the role contracts: only the Owner role exists,
    // as a membership with the Owner flag. Any other role is ignored.
    public class UserRoleAssigner : IUserRoleAssigner
    {
        private readonly IdentityDbContext _db;

        public UserRoleAssigner(IdentityDbContext db)
        {
            _db = db;
        }

        public async Task AssignRoleAsync(Guid userId, Guid organizationId, string role)
        {
            if (role != Roles.Owner)
                return;

            _db.OrganizationMembers.Add(new OrganizationMember
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OrganizationId = organizationId,
                IsOwner = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        public async Task RevokeRoleAsync(Guid userId, Guid organizationId, string role)
        {
            if (role != Roles.Owner)
                return;

            var member = await _db.OrganizationMembers.FirstOrDefaultAsync(m =>
                m.UserId == userId && m.OrganizationId == organizationId && m.IsOwner);
            if (member is null)
                return;

            _db.OrganizationMembers.Remove(member);
            await _db.SaveChangesAsync();
        }
    }
}
