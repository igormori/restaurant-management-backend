using System.Net;

namespace RestaurantManagement.Web.Tests;

// TestServer has no real socket, so tests choose the connection's remote IP with a header.
public class TestRemoteIpStartupFilter : IStartupFilter
{
    public const string RemoteIpHeader = "X-Test-Remote-IP";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                if (context.Request.Headers.TryGetValue(RemoteIpHeader, out var value))
                {
                    context.Connection.RemoteIpAddress = IPAddress.Parse(value.ToString());
                }

                await nextMiddleware();
            });
            next(app);
        };
    }
}
