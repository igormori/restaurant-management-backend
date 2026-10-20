using System.Runtime.CompilerServices;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Settings every test host needs, set once before any test runs so each factory picks them up
/// through the environment-variable configuration source.
/// </summary>
internal static class TestHostSettings
{
    [ModuleInitializer]
    internal static void Apply()
    {
        // The cleanup job would otherwise delete rows from whatever database the host points at,
        // including a developer's local database.
        Environment.SetEnvironmentVariable("RefreshTokenCleanup__Enabled", "false");

        // The pending email worker would otherwise send queued emails on its own timer, making endpoint
        // tests unpredictable; tests run the processor themselves.
        Environment.SetEnvironmentVariable("PendingEmails__Enabled", "false");
    }
}
