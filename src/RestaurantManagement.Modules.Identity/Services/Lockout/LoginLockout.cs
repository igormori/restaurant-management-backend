using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities.Lockout;
using RestaurantManagement.Modules.Identity.Entities.Registration;
using RestaurantManagement.Modules.Identity.Entities.Users;
using RestaurantManagement.Shared.Options;

namespace RestaurantManagement.Modules.Identity.Services.Lockout
{
    public enum LockOutcome
    {
        None,
        AddressLocked,
        AccountLocked
    }

    // Per-address and whole-account login lock, decided from one stored row per counted wrong password.
    public class LoginLockout
    {
        private static readonly TimeSpan LockEmailInterval = TimeSpan.FromHours(24);
        private static readonly Guid NoUserId = Guid.Empty;

        private readonly IdentityDbContext _db;
        private readonly SecurityOptions _options;
        private readonly TimeProvider _time;

        public LoginLockout(IdentityDbContext db, SecurityOptions options, TimeProvider? timeProvider = null)
        {
            _db = db;
            _options = options;
            _time = timeProvider ?? TimeProvider.System;
        }

        // True when the account lock or the lock of this address applies to the attempt.
        public async Task<bool> IsLockedAsync(User user, string? ipAddress)
        {
            var now = Now();
            if (user.LockedUntil.HasValue && user.LockedUntil > now)
                return true;

            return await IsAddressLockedAsync(user.Id, Normalize(ipAddress), now);
        }

        // Runs the same database commands as a wrong password for a real account, on a user ID that matches
        // no row and so changes nothing. An unknown email then takes as long as a known one.
        public async Task SimulateFailureAsync(string? ipAddress)
        {
            var now = Now();
            var address = Normalize(ipAddress);

            await IsAddressLockedAsync(NoUserId, address, now);
            await DeleteExpiredAsync(now);
            await _db.LoginFailures
                .Where(f => f.UserId == NoUserId)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.CountsForAddress, true));
            await LockAddressWhenLimitReachedAsync(NoUserId, address, now);
            await LockAccountWhenLimitReachedAsync(NoUserId, now);
        }

        private Task<bool> IsAddressLockedAsync(Guid userId, string address, DateTime now)
            => _db.LoginFailures.AnyAsync(f =>
                f.UserId == userId && f.IpAddress == address && f.AddressLockedUntil > now);

        // Stores one counted wrong password and reports which lock, if any, this request set.
        public async Task<LockOutcome> RecordFailureAsync(User user, string? ipAddress)
        {
            var now = Now();
            var address = Normalize(ipAddress);

            await DeleteExpiredAsync(now);

            _db.LoginFailures.Add(new LoginFailure
            {
                UserId = user.Id,
                IpAddress = address,
                FailedAt = now,
                CountsForAddress = true,
                CountsForAccount = true
            });
            await _db.SaveChangesAsync();

            var addressLocked = await LockAddressWhenLimitReachedAsync(user.Id, address, now);
            if (await LockAccountWhenLimitReachedAsync(user.Id, now))
                return LockOutcome.AccountLocked;

            return addressLocked ? LockOutcome.AddressLocked : LockOutcome.None;
        }

        // A correct password stops this account's rows for this address counting for the address only.
        public async Task ClearAddressAsync(User user, string? ipAddress)
        {
            var address = Normalize(ipAddress);
            await _db.LoginFailures
                .Where(f => f.UserId == user.Id && f.IpAddress == address && f.CountsForAddress)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.CountsForAddress, false));
        }

        private async Task<bool> LockAddressWhenLimitReachedAsync(Guid userId, string address, DateTime now)
        {
            var counted = await _db.LoginFailures
                .Where(f => f.UserId == userId && f.IpAddress == address && f.CountsForAddress)
                .OrderBy(f => f.FailedAt)
                .ToListAsync();
            if (counted.Count < _options.MaxFailedLoginAttempts)
                return false;

            counted.ForEach(f => f.CountsForAddress = false);
            counted[^1].AddressLockedUntil = now.AddMinutes(_options.LockoutDurationMinutes);
            await _db.SaveChangesAsync();
            return true;
        }

        // Each address adds at most MaxFailedLoginAttempts, so one address alone can never lock the account.
        private async Task<bool> LockAccountWhenLimitReachedAsync(Guid userId, DateTime now)
        {
            var since = now.AddMinutes(-_options.AccountFailedLoginWindowMinutes);
            var perAddress = await _db.LoginFailures
                .Where(f => f.UserId == userId && f.CountsForAccount && f.FailedAt >= since)
                .GroupBy(f => f.IpAddress)
                .Select(g => g.Count())
                .ToListAsync();
            if (perAddress.Sum(count => Math.Min(count, _options.MaxFailedLoginAttempts)) < _options.AccountMaxFailedLoginAttempts)
                return false;

            // The lock, the counter reset, the email mark and the queue row commit together: a failure
            // in between must not leave the account locked and marked as emailed with no email queued.
            await using var transaction = await _db.Database.BeginTransactionAsync();

            // Only one concurrent request wins the update, so only one reports the lock.
            var lockEnd = now.AddMinutes(_options.LockoutDurationMinutes);
            var claimed = await _db.Users
                .Where(u => u.Id == userId && (u.LockedUntil == null || u.LockedUntil <= now))
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.LockedUntil, lockEnd));
            if (claimed == 0)
                return false;

            await _db.LoginFailures
                .Where(f => f.UserId == userId && f.CountsForAccount)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.CountsForAccount, false));
            await QueueLockedEmailAsync(userId, now);
            await transaction.CommitAsync();
            return true;
        }

        // At most one email per account per LockEmailInterval; the stored time is the guard, not the queue row,
        // because the row is deleted once the email is sent. Only the lock winner reaches this.
        private async Task QueueLockedEmailAsync(Guid userId, DateTime now)
        {
            var emailCutoff = now - LockEmailInterval;
            var email = await _db.Users
                .Where(u => u.Id == userId && (u.AccountLockedEmailAt == null || u.AccountLockedEmailAt <= emailCutoff))
                .Select(u => u.Email)
                .FirstOrDefaultAsync();
            if (email == null)
                return;

            var marked = await _db.Users
                .Where(u => u.Id == userId && (u.AccountLockedEmailAt == null || u.AccountLockedEmailAt <= emailCutoff))
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.AccountLockedEmailAt, now));
            if (marked == 0)
                return;

            _db.PendingEmails.Add(new PendingEmail { Purpose = PendingEmailPurpose.AccountLocked, Email = email, CreatedAt = now, NextAttemptAt = now });
            await _db.SaveChangesAsync();
        }

        private Task<int> DeleteExpiredAsync(DateTime now)
        {
            var cutoff = now.AddMinutes(-_options.AccountFailedLoginWindowMinutes);
            return _db.LoginFailures
                .Where(f => f.FailedAt < cutoff && (f.AddressLockedUntil == null || f.AddressLockedUntil <= now))
                .ExecuteDeleteAsync();
        }

        private DateTime Now() => _time.GetUtcNow().UtcDateTime;

        private static string Normalize(string? ipAddress)
            => string.IsNullOrWhiteSpace(ipAddress)
                ? LoginFailure.UnknownAddress
                : ipAddress.Length > LoginFailure.IpAddressMaxLength ? ipAddress[..LoginFailure.IpAddressMaxLength] : ipAddress;
    }
}
