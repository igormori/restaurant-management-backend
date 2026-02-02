using System.Net;
using System.Text.Json;
using Sentry;
using RestaurantManagement.Api.Utils.Exceptions;

namespace RestaurantManagement.Api.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;
        private readonly IHostEnvironment _env;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (BusinessException bex)
            {
                // ✅ Business error: skip Sentry
                _logger.LogWarning(bex, "Business exception");

                context.Response.StatusCode = bex.StatusCode;
                context.Response.ContentType = "application/json";

                var errorResponse = new { error = bex.Message };
                await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
            }
            catch (Exception ex)
            {
                // ✅ Unexpected error: send to Sentry
                SentrySdk.CaptureException(ex);
                _logger.LogError(ex, "Unhandled exception");

                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.ContentType = "application/json";

                var errorResponse = new
                {
                    error = "An unexpected error occurred.",
                    detail = _env.IsDevelopment() ? ex.Message : null
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
            }
        }
        
    }
}