using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RestaurantManagement.Shared.Middleware;
using RestaurantManagement.Shared.Utils.Exceptions;

namespace RestaurantManagement.Shared.Tests
{
    public class ExceptionHandlingMiddlewareTests
    {
        private const string MessageProperty = "message";
        private const string BusinessErrorMessage = "Invalid email or password";
        private const int UnauthorizedStatusCode = 401;
        private const int InternalServerErrorStatusCode = 500;

        private static async Task<(int StatusCode, JsonElement Body)> InvokeAsync(Exception exception)
        {
            var environment = Substitute.For<IHostEnvironment>();
            environment.EnvironmentName.Returns(Environments.Production);
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

        [Fact]
        [Trait("Rule", "SH18")]
        public async Task InvokeAsync_UnexpectedException_ReturnsMessageProperty()
        {
            // Act
            var (statusCode, body) = await InvokeAsync(new InvalidOperationException("boom"));

            // Assert
            statusCode.Should().Be(InternalServerErrorStatusCode);
            body.TryGetProperty(MessageProperty, out _).Should().BeTrue();
        }

        [Fact]
        [Trait("Rule", "SH17")]
        public async Task InvokeAsync_BusinessException_ReturnsMessagePropertyWithExceptionMessage()
        {
            // Act
            var (statusCode, body) = await InvokeAsync(new BusinessException(BusinessErrorMessage, UnauthorizedStatusCode));

            // Assert
            statusCode.Should().Be(UnauthorizedStatusCode);
            body.GetProperty(MessageProperty).GetString().Should().Be(BusinessErrorMessage);
        }
    }
}
