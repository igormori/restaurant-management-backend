using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using RestaurantManagement.Web.Hosting;
using Shouldly;

namespace RestaurantManagement.Web.Tests;

public class KeyVaultHostingExtensionsTests
{
    private const string VaultUriKey = "KeyVault:Uri";

    private static WebApplicationBuilder CreateBuilder(string? vaultUri)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { [VaultUriKey] = vaultUri });
        return builder;
    }

    [Theory]
    [InlineData("http://restaurant-kv.vault.azure.net/")]
    [InlineData("restaurant-kv.vault.azure.net")]
    [Trait("Rule", "W17")]
    public void AddKeyVaultSecrets_UriNotAbsoluteHttps_ThrowsNamingKeyVaultUri(string vaultUri)
    {
        // Arrange
        var builder = CreateBuilder(vaultUri);

        // Act
        var act = () => builder.AddKeyVaultSecrets();

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message.ShouldContain(VaultUriKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Rule", "W17")]
    public void AddKeyVaultSecrets_UriNotSet_AddsNoConfigurationSource(string? vaultUri)
    {
        // Arrange
        var builder = CreateBuilder(vaultUri);
        var sourceCountBefore = ((IConfigurationBuilder)builder.Configuration).Sources.Count;

        // Act
        builder.AddKeyVaultSecrets();

        // Assert
        ((IConfigurationBuilder)builder.Configuration).Sources.Count.ShouldBe(sourceCountBefore);
    }
}
