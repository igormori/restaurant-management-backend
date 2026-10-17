using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit.Sdk;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Starts a test host that must refuse to start, and returns the error that stopped it.
/// When the app's startup checks fail, the app's own thread disposes the half-built host. If that
/// happens before WebApplicationFactory reads the host, the test sees "Cannot access a disposed
/// object" instead of the real error, which made startup tests fail at random in busy full runs.
/// The startup check's error is therefore also recorded where it is raised, before any disposal.
/// </summary>
internal static class StartupFailure
{
    public static Exception Capture(WebApplicationFactory<Program> factory)
    {
        var recorder = new StartupErrorRecorder();
        using var recording = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => RecordStartupValidation(services, recorder)));
        try
        {
            recording.CreateClient();
        }
        catch (Exception thrown)
        {
            return recorder.Error ?? thrown;
        }

        throw new XunitException("The app started, but it was expected to refuse to start.");
    }

    private static void RecordStartupValidation(IServiceCollection services, StartupErrorRecorder recorder)
    {
        var original = services.LastOrDefault(d => d.ServiceType == typeof(IStartupValidator));
        if (original is null)
            return;

        services.Remove(original);
        services.AddTransient<IStartupValidator>(provider =>
            new RecordingStartupValidator(Resolve(provider, original), recorder));
    }

    private static IStartupValidator Resolve(IServiceProvider provider, ServiceDescriptor original)
        => (IStartupValidator)(original.ImplementationInstance
            ?? original.ImplementationFactory?.Invoke(provider)
            ?? ActivatorUtilities.CreateInstance(provider, original.ImplementationType!));

    private sealed class StartupErrorRecorder
    {
        public Exception? Error { get; set; }
    }

    private sealed class RecordingStartupValidator(IStartupValidator inner, StartupErrorRecorder recorder) : IStartupValidator
    {
        public void Validate()
        {
            try
            {
                inner.Validate();
            }
            catch (Exception error)
            {
                recorder.Error = error;
                throw;
            }
        }
    }
}
