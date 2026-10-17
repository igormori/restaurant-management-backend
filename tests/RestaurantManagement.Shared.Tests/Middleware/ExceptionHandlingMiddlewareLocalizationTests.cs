using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RestaurantManagement.Shared.Middleware;
using Shouldly;

namespace RestaurantManagement.Shared.Tests.Middleware;

/// <summary>
/// The unexpected-error answer (500) in the language of the request. The middleware reads the
/// message catalog from the request's services, the way the app's sign-in events do.
/// </summary>
public class ExceptionHandlingMiddlewareLocalizationTests : IDisposable
{
    private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;

    public void Dispose() => CultureInfo.CurrentUICulture = _originalUiCulture;

    private static async Task<(int StatusCode, JsonElement Body)> InvokeAsync(Exception exception, string environmentName)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw exception,
            NullLogger<ExceptionHandlingMiddleware>.Instance,
            environment);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization(options => options.ResourcesPath = "Localization");
        var context = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);
        return (context.Response.StatusCode, body);
    }

    // Scenario 4
    [Theory]
    [InlineData("pt", "Ocorreu um erro inesperado.")]
    [InlineData("en", "An unexpected error occurred.")]
    [Trait("Rule", "SH18")]
    public async Task InvokeAsync_UnexpectedExceptionInRequestCulture_ReturnsLocalizedUnexpectedErrorMessage(string culture, string expected)
    {
        // Arrange
        CultureInfo.CurrentUICulture = new CultureInfo(culture);

        // Act
        var (statusCode, body) = await InvokeAsync(new InvalidOperationException("Kitchen printer queue unavailable"), Environments.Production);

        // Assert
        statusCode.ShouldBe(StatusCodes.Status500InternalServerError, "an unexpected error answers with a server error status");
        body.GetProperty("message").GetString().ShouldBe(expected, "the unexpected error message is shown in the language of the request");
    }

    // Scenario 5
    [Fact]
    [Trait("Rule", "SH18")]
    public async Task InvokeAsync_UnexpectedExceptionInProduction_HidesExceptionDetail()
    {
        // Arrange
        CultureInfo.CurrentUICulture = new CultureInfo("pt");

        // Act
        var (_, body) = await InvokeAsync(new InvalidOperationException("Connection string Host=db-prod-cantina"), Environments.Production);

        // Assert
        body.GetProperty("detail").ValueKind.ShouldBe(JsonValueKind.Null, "the error detail is hidden in production");
        body.GetRawText().ShouldNotContain("db-prod-cantina", customMessage: "internal details such as connection settings never reach the caller");
    }
}
