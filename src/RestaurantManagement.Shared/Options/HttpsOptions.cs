namespace RestaurantManagement.Shared.Options
{
    public class HttpsOptions
    {
        public int Port { get; set; }
        public string CertificatePath { get; set; } = string.Empty;
        public string CertificatePassword { get; set; } = string.Empty;
    }
}
