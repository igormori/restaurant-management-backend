using System.Net;
using RestaurantManagement.Shared.Options;

namespace RestaurantManagement.Web.Hosting
{
    public static class HttpsHostingExtensions
    {
        private const string ConfigurationSection = "Https";
        private const string CertificatePathKey = "Https:CertificatePath";
        private const int DefaultHttpsPort = 8443;

        public static WebApplicationBuilder UseHttpsOnlyOutsideDevelopment(this WebApplicationBuilder builder)
        {
            if (builder.Environment.IsDevelopment()) return builder;

            var options = builder.Configuration.GetSection(ConfigurationSection).Get<HttpsOptions>() ?? new HttpsOptions();
            ValidateCertificate(options);

            var port = options.Port > 0 ? options.Port : DefaultHttpsPort;

            // Explicit Listen replaces any URL configured elsewhere (e.g. ASPNETCORE_URLS), so plain HTTP is never served.
            builder.WebHost.ConfigureKestrel(kestrel =>
                kestrel.Listen(IPAddress.Any, port, listen =>
                    listen.UseHttps(options.CertificatePath, options.CertificatePassword)));

            return builder;
        }

        private static void ValidateCertificate(HttpsOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.CertificatePath))
                throw new InvalidOperationException($"{CertificatePathKey} is required outside Development.");

            if (!File.Exists(options.CertificatePath))
                throw new InvalidOperationException($"{CertificatePathKey} points to a file that does not exist.");
        }
    }
}
