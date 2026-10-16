namespace RestaurantManagement.Shared.Options
{
    public class AuthRateLimitOptions
    {
        public EndpointRateLimit Login { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };
        public EndpointRateLimit Register { get; set; } = new() { PermitLimit = 5, WindowSeconds = 3600 };
        public EndpointRateLimit ResendVerification { get; set; } = new() { PermitLimit = 3, WindowSeconds = 600 };
        public EndpointRateLimit Verify { get; set; } = new() { PermitLimit = 10, WindowSeconds = 600 };
        public EndpointRateLimit Refresh { get; set; } = new() { PermitLimit = 30, WindowSeconds = 60 };
        public EndpointRateLimit ForgotPassword { get; set; } = new() { PermitLimit = 3, WindowSeconds = 600 };
        public EndpointRateLimit ResetPassword { get; set; } = new() { PermitLimit = 10, WindowSeconds = 600 };
        public EndpointRateLimit ChangePassword { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };
        public string[] TrustedProxies { get; set; } = Array.Empty<string>();
    }

    public class EndpointRateLimit
    {
        public int PermitLimit { get; set; }
        public int WindowSeconds { get; set; }
    }
}
