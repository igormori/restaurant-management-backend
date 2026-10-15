using Azure.Identity;

namespace RestaurantManagement.Web.Hosting
{
    public static class KeyVaultHostingExtensions
    {
        private const string VaultUriKey = "KeyVault:Uri";

        // Must run before any configuration is read, so vault secrets override appsettings.json.
        // Vault secret names use "--" for nesting: "Jwt--Key" becomes Jwt:Key.
        public static WebApplicationBuilder AddKeyVaultSecrets(this WebApplicationBuilder builder)
        {
            var vaultUri = builder.Configuration[VaultUriKey];
            if (string.IsNullOrWhiteSpace(vaultUri)) return builder;

            if (!Uri.TryCreate(vaultUri, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException($"{VaultUriKey} must be an absolute https URI.");

            // Managed identity in Azure, az login locally; one code path for both.
            builder.Configuration.AddAzureKeyVault(uri, new DefaultAzureCredential());

            return builder;
        }
    }
}
