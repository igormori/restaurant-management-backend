using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantManagement.Modules.Identity.Entities
{
    // One pending "forgot password" request. It has no user: the email is stored as typed and
    // only the processor decides whether the email belongs to an account that may get a code.
    public class PasswordResetRequest
    {
        public Guid Id { get; set; }
        // STUB (tester): [NotMapped] keeps the model equal to the migrations until T1 adds the column.
        // T1 removes [NotMapped], makes the column required and generates the migration.
        [NotMapped]
        public PasswordResetRequestPurpose Purpose { get; set; } = PasswordResetRequestPurpose.PasswordReset;
        public string Email { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int Attempts { get; set; }
        public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
        public DateTime? ClaimedUntil { get; set; }
        public DateTime? FailedAt { get; set; }
        // Set together with the new code and cleared when a send fails; still set on a claimed row
        // means a pass crashed after storing the code, so whether the email went out is unknown.
        public DateTime? CodeIssuedAt { get; set; }
    }
}
