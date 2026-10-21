using Microsoft.EntityFrameworkCore;
using Npgsql;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities.Invitations;
using RestaurantManagement.Modules.Identity.Entities.Members;
using RestaurantManagement.Modules.Identity.Entities.Users;
using RestaurantManagement.Modules.Identity.Models.Invitations;
using RestaurantManagement.Shared.Utils.Exceptions;

namespace RestaurantManagement.Modules.Identity.Services.Invitations
{
    // Accepting an invitation: the token carries no organization, so the invitation is found by its
    // fingerprint alone (Identity has no query filter) and the rows are written under its tenant scope.
    public partial class InvitationService
    {
        public async Task AcceptAsync(AcceptInvitationRequest request)
        {
            var invitation = await FindOpenAsync(request.Token);
            var userId = _currentUser.UserId ?? throw new BusinessException(_localizer["AccessDenied"].Value, 403);
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId)
                ?? throw new BusinessException(_localizer["AccessDenied"].Value, 403);
            if (!string.Equals(user.Email, invitation.Email, StringComparison.OrdinalIgnoreCase))
                throw new BusinessException(_localizer["InvitationForAnotherAccount"].Value, 403);

            await JoinAsync(invitation, user.Id, newUser: null);
        }

        public async Task AcceptWithNewAccountAsync(AcceptInvitationWithAccountRequest request)
        {
            var invitation = await FindOpenAsync(request.Token);
            if (await _db.Users.AnyAsync(u => u.Email.ToLower() == invitation.Email))
                throw new BusinessException(_localizer["InvitationAccountExistsSignIn"].Value, 400);

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = invitation.Email,
                PasswordHash = _passwordHasher.Hash(request.Password),
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                IsActive = true,
                IsVerified = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            try
            {
                await JoinAsync(invitation, user.Id, user);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { ConstraintName: IdentityDbContext.UniqueEmailIndexName })
            {
                throw new BusinessException(_localizer["InvitationAccountExistsSignIn"].Value, 400);
            }
        }

        // Unknown, expired, used, replaced and cancelled links all give the same answer.
        private async Task<OrganizationInvitation> FindOpenAsync(string token)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var hash = InvitationTokens.Hash(token ?? string.Empty);
            return await _db.OrganizationInvitations.AsNoTracking().FirstOrDefaultAsync(i => i.TokenHash == hash
                    && i.UsedAt == null && i.CancelledAt == null && i.ReplacedAt == null && i.ExpiresAt > now)
                ?? throw new BusinessException(_localizer["InvitationLinkInvalid"].Value, 400);
        }

        private async Task JoinAsync(OrganizationInvitation invitation, Guid userId, User? newUser)
        {
            using var scope = _tenantScope.Begin(invitation.OrganizationId);
            await using var transaction = await _db.Database.BeginTransactionAsync();
            if (await _db.OrganizationMembers.AnyAsync(m => m.UserId == userId && m.OrganizationId == invitation.OrganizationId))
                throw new BusinessException(_localizer["AlreadyMemberOfOrganization"].Value, 400);

            // One conditional update claims the link, so two requests at once join once.
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var claimed = await _db.OrganizationInvitations.Where(i => i.Id == invitation.Id
                    && i.UsedAt == null && i.CancelledAt == null && i.ReplacedAt == null && i.ExpiresAt > now)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.UsedAt, now).SetProperty(i => i.UpdatedAt, now));
            if (claimed != 1)
                throw new BusinessException(_localizer["InvitationLinkInvalid"].Value, 400);

            if (newUser is not null)
                _db.Users.Add(newUser);
            await AddMembershipAsync(invitation, userId, now);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        private async Task AddMembershipAsync(OrganizationInvitation invitation, Guid userId, DateTime now)
        {
            _db.OrganizationMembers.Add(new OrganizationMember
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OrganizationId = invitation.OrganizationId,
                IsOwner = false,
                CreatedAt = now,
                UpdatedAt = now
            });
            var levels = await _db.OrganizationInvitationLevels.Where(l => l.InvitationId == invitation.Id).ToListAsync();
            var roles = await _db.OrganizationInvitationRoles.Where(r => r.InvitationId == invitation.Id).ToListAsync();
            _db.MemberLocationLevels.AddRange(levels.Select(l => new MemberLocationLevel
            {
                Id = Guid.NewGuid(),
                OrganizationId = invitation.OrganizationId,
                UserId = userId,
                LocationId = l.LocationId,
                Module = l.Module,
                Level = l.Level,
                CreatedAt = now,
                UpdatedAt = now
            }));
            _db.MemberLocationRoles.AddRange(roles.Select(r => new MemberLocationRole
            {
                Id = Guid.NewGuid(),
                OrganizationId = invitation.OrganizationId,
                UserId = userId,
                LocationId = r.LocationId,
                RoleId = r.RoleId,
                CreatedAt = now,
                UpdatedAt = now
            }));
        }
    }
}
