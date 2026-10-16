namespace RestaurantManagement.Shared.RateLimiting
{
    public static class AuthRateLimitPolicies
    {
        public const string Login = "auth-login";
        public const string Register = "auth-register";
        public const string Refresh = "auth-refresh";
        public const string Verify = "auth-verify";
        public const string ResendVerification = "auth-resend-verification";
        public const string ForgotPassword = "auth-forgot-password";
        public const string ResetPassword = "auth-reset-password";
        public const string ChangePassword = "auth-change-password";
    }
}
