namespace RestaurantManagement.Shared.RateLimiting
{
    public static class AuthRateLimitPolicies
    {
        public const string Login = "auth-login";
        public const string Register = "auth-register";
        public const string Refresh = "auth-refresh";
        public const string Verify = "auth-verify";
        public const string ResendVerification = "auth-resend-verification";
    }
}
