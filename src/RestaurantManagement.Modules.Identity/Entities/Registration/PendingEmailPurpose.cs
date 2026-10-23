namespace RestaurantManagement.Modules.Identity.Entities.Registration
{
    // What a pending email is for. There is no zero value, so a row with no purpose is never valid.
    public enum PendingEmailPurpose
    {
        PasswordReset = 1,
        Verification = 2,
        AccountLocked = 3
    }
}
