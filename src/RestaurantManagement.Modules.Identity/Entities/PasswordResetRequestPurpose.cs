namespace RestaurantManagement.Modules.Identity.Entities
{
    // What a queued request is for. There is no zero value, so a row with no purpose is never valid.
    public enum PasswordResetRequestPurpose
    {
        PasswordReset = 1,
        Verification = 2
    }
}
