namespace RestaurantManagement.Modules.Identity.Entities
{
    // What a pending email is for. There is no zero value, so a row with no purpose is never valid.
    public enum PendingEmailPurpose
    {
        PasswordReset = 1,
        Verification = 2
    }
}
