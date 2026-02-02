namespace RestaurantManagement.Api.Options
{
    public class CorsOptions
    {
        public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
    }
}
