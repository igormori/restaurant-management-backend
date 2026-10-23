namespace RestaurantManagement.Modules.Identity.Entities.Registration
{
    // One email waiting to be sent, of any purpose. It has no user: the email is stored as typed and
    // only the processor decides whether the email belongs to an account that may get a code.
    public class PendingEmail
    {
        public Guid Id { get; set; }
        public PendingEmailPurpose Purpose { get; set; } = PendingEmailPurpose.PasswordReset;
        public string Email { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int Attempts { get; set; }
        public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
        public DateTime? ClaimedUntil { get; set; }
        public DateTime? FailedAt { get; set; }
        // Set together with the new code and cleared when a send fails; still set on a claimed row
        // means a pass crashed after storing the code, so whether the email went out is unknown.
        public DateTime? CodeIssuedAt { get; set; }
        // For emails without a code (account locked): set just before the send and cleared when it fails;
        // still set on a claimed row means a pass crashed mid-send, so the email is dropped, not sent twice.
        public DateTime? SendStartedAt { get; set; }
    }
}
