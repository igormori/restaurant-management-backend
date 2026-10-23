using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities.Lockout;
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

            var address = Normalize(ipAddress);
            return await _db.LoginFailures.AnyAsync(f =>
                f.UserId == user.Id && f.IpAddress == address && f.AddressLockedUntil > now);
        }

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
            if (await LockAccountWhenLimitReachedAsync(user, now))
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
        private async Task<bool> LockAccountWhenLimitReachedAsync(User user, DateTime now)
        {
            var since = now.AddMinutes(-_options.AccountFailedLoginWindowMinutes);
            var perAddress = await _db.LoginFailures
                .Where(f => f.UserId == user.Id && f.CountsForAccount && f.FailedAt >= since)
                .GroupBy(f => f.IpAddress)
                .Select(g => g.Count())
                .ToListAsync();
            if (perAddress.Sum(count => Math.Min(count, _options.MaxFailedLoginAttempts)) < _options.AccountMaxFailedLoginAttempts)
                return false;

            // Only one concurrent request wins the update, so only one reports the lock.
            var lockEnd = now.AddMinutes(_options.LockoutDurationMinutes);
            var claimed = await _db.Users
                .Where(u => u.Id == user.Id && (u.LockedUntil == null || u.LockedUntil <= now))
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.LockedUntil, lockEnd));
            if (claimed == 0)
                return false;

            await _db.LoginFailures
                .Where(f => f.UserId == user.Id && f.CountsForAccount)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.CountsForAccount, false));
            return true;
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
