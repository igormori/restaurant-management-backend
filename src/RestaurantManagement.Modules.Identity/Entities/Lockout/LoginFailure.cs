namespace RestaurantManagement.Modules.Identity.Entities.Lockout
{
    // One counted wrong password for an account, from one IP (Internet Protocol) address.
    public class LoginFailure
    {
        public const int IpAddressMaxLength = 45;
        public const string UnknownAddress = "unknown";

        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string IpAddress { get; set; } = UnknownAddress;
        public DateTime FailedAt { get; set; }
        public bool CountsForAddress { get; set; }
        public bool CountsForAccount { get; set; }
        public DateTime? AddressLockedUntil { get; set; }
    }
}
