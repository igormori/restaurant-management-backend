using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities.Users;
using RestaurantManagement.Shared.Options;

namespace RestaurantManagement.Modules.Identity.Services.Lockout
{
    // Stub (tester): per-address and whole-account login lock (task T1).
    public class LoginLockout
    {
        public LoginLockout(IdentityDbContext db, SecurityOptions options, TimeProvider? timeProvider = null)
        {
        }

        // True when a lock (address or account) applies to this attempt.
        public Task<bool> IsLockedAsync(User user, string? ipAddress)
            => throw new NotImplementedException();

        // Stores one counted wrong password. True when this request locked the whole account.
        public Task<bool> RecordFailureAsync(User user, string? ipAddress)
            => throw new NotImplementedException();

        // A correct password stops this account's rows for this address counting for the address.
        public Task ClearAddressAsync(User user, string? ipAddress)
            => throw new NotImplementedException();
    }
}
