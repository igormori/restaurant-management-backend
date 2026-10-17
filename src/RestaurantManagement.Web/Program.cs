using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.Text;
using Sentry;

// Shared
using RestaurantManagement.Shared;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Middleware;
using RestaurantManagement.Shared.Options;
using RestaurantManagement.Shared.Services.AuditLog;
using RestaurantManagement.Shared.Services.Email;
using RestaurantManagement.Shared.Services.Identity;
using RestaurantManagement.Shared.Services.Organization;
using RestaurantManagement.Shared.Utils.Localization;
using RestaurantManagement.Web.Auth;
using RestaurantManagement.Web.Hosting;
using RestaurantManagement.Web.RateLimiting;

// Modules
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Modules.Menu.Data;
using RestaurantManagement.Modules.Menu.Services;
using RestaurantManagement.Modules.Organization.Data;
using RestaurantManagement.Modules.Organization.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------
// Azure Key Vault (before any configuration is read)
// ---------------------------------------------
builder.AddKeyVaultSecrets();

// Outside Development: HTTPS only, and startup fails without a certificate
builder.UseHttpsOnlyOutsideDevelopment();

// ---------------------------------------------
// Add Localization
// ---------------------------------------------
builder.Services.AddLocalization(options => options.ResourcesPath = "Localization");

var supportedCultures = new[]
{
    new CultureInfo("en"),
    new CultureInfo("pt"),
    new CultureInfo("pt-BR"),
    new CultureInfo("pt-PT")
};

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("en");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.RequestCultureProviders.Insert(0, new CustomPortugueseCultureProvider());
});

// ---------------------------------------------
// JWT Authentication
// ---------------------------------------------
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt section is missing");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtOptions.Issuer,
        ValidAudience = jwtOptions.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
        // Only the algorithm the app signs with, so a token cannot pick a different one.
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        // The framework default gives 5 minutes past expiry; access tokens are short-lived, so none.
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = async ctx =>
        {
            ctx.NoResult();
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            ctx.Response.ContentType = "application/json";
            var loc = ctx.HttpContext.RequestServices.GetRequiredService<IStringLocalizer<SharedResource>>();
            var msg = loc["InvalidToken"].Value;
            await ctx.Response.WriteAsJsonAsync(new { message = msg });
        },
        OnChallenge = async ctx =>
        {
            ctx.HandleResponse();
            if (!ctx.Response.HasStarted)
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                ctx.Response.ContentType = "application/json";
                var loc = ctx.HttpContext.RequestServices.GetRequiredService<IStringLocalizer<SharedResource>>();
                var msg = loc["UnauthorizedMessage"].Value;
                await ctx.Response.WriteAsJsonAsync(new { message = msg });
            }
        },
        OnForbidden = async ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            ctx.Response.ContentType = "application/json";
            var loc = ctx.HttpContext.RequestServices.GetRequiredService<IStringLocalizer<SharedResource>>();
            var msg = loc["AccessDenied"].Value;
            await ctx.Response.WriteAsJsonAsync(new { message = msg });
        }
    };
});

builder.Services.AddPermissionPolicies();
builder.Services.AddLocalizedValidationTitle();

// ---------------------------------------------
// Controllers
// ---------------------------------------------
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    })
    .AddDataAnnotationsLocalization(options =>
    {
        options.DataAnnotationLocalizerProvider = (type, factory) =>
            factory.Create(typeof(SharedResource));
    })
    .AddApplicationPart(typeof(RestaurantManagement.Shared.Controllers.AuditLogController).Assembly)
    .AddApplicationPart(typeof(RestaurantManagement.Modules.Identity.Controllers.AuthController).Assembly)
    .AddApplicationPart(typeof(RestaurantManagement.Modules.Menu.Controllers.MenuController).Assembly)
    .AddApplicationPart(typeof(RestaurantManagement.Modules.Organization.Controllers.OrganizationController).Assembly);

// ---------------------------------------------
// Options Configuration
// ---------------------------------------------
builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection("Security"));
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
builder.Services.AddOptions<VerificationCodeOptions>()
    .Bind(builder.Configuration.GetSection("VerificationCode"))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<VerificationCodeOptions>, VerificationCodeOptionsValidator>();
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<CorsOptions>(builder.Configuration.GetSection("Cors"));
builder.Services.AddAuthRateLimiting(builder.Configuration);

// ---------------------------------------------
// Database Contexts (Per Module)
// ---------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ITenantScope, TenantScope>();
builder.Services.AddScoped<ICurrentUser, ClaimsCurrentUser>();
builder.Services.AddScoped<SelectedLocation>();
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();

builder.Services.AddDbContext<IdentityDbContext>((serviceProvider, options) =>
    options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention()
        .AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>()));

builder.Services.AddDbContext<MenuDbContext>((serviceProvider, options) =>
    options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention()
        .AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>()));

builder.Services.AddDbContext<OrganizationDbContext>((serviceProvider, options) =>
    options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention()
        .AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>()));

builder.Services.AddAuditLog((sp, options) => options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention()
    .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));

// ---------------------------------------------
// Sentry for error tracking
// ---------------------------------------------
builder.AddSentryTracking();

// ---------------------------------------------
// Swagger/OpenAPI
// ---------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Restaurant Management API",
        Version = "v1",
        Description = "Modular Monolith API for Restaurant Management"
    });

    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "Paste the access token from /api/auth/login (without the 'Bearer' prefix).",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ---------------------------------------------
// Dependency Injection: Register Services
// ---------------------------------------------
// Shared Services
builder.Services.AddScoped<IEmailService, EmailService>();

// Identity Module Services
builder.Services.AddScoped<IRegistrationService, RegistrationService>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<IRefreshTokenCleanupService, RefreshTokenCleanupService>();
if (builder.Configuration.GetValue("RefreshTokenCleanup:Enabled", true))
{
    builder.Services.AddHostedService<RefreshTokenCleanupWorker>();
}
builder.Services.AddScoped<IVerificationService, VerificationService>();
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
builder.Services.AddScoped<IPasswordResetEmailProcessor, PasswordResetEmailProcessor>();
if (builder.Configuration.GetValue("PasswordResetEmails:Enabled", true))
{
    builder.Services.AddHostedService<PasswordResetEmailWorker>();
}
builder.Services.AddScoped<IPasswordChangeService, PasswordChangeService>();
builder.Services.AddScoped<IPasswordAuditRecorder, PasswordAuditRecorder>();
builder.Services.AddSingleton<IVerificationCodeHasher, VerificationCodeHasher>();
builder.Services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();

// Organization Module Services
builder.Services.AddScoped<IOrganizationService, OrganizationService>();
builder.Services.AddScoped<ILocationService, LocationService>();

// Menu Module Services
builder.Services.AddScoped<RestaurantManagement.Modules.Menu.Services.IMenuService, RestaurantManagement.Modules.Menu.Services.MenuService>();
builder.Services.AddScoped<RestaurantManagement.Modules.Menu.Services.IMenuCategoryService, RestaurantManagement.Modules.Menu.Services.MenuCategoryService>();

// Cross-Module Contracts (Shared interface, implemented by the owning module)
builder.Services.AddScoped<IOrganizationLookup, RestaurantManagement.Modules.Organization.Services.OrganizationLookup>();
builder.Services.AddScoped<IUserRoleLookup, RestaurantManagement.Modules.Identity.Services.UserRoleLookup>();
builder.Services.AddScoped<IUserRoleAssigner, RestaurantManagement.Modules.Identity.Services.UserRoleAssigner>();

// ---------------------------------------------
// CORS
// ---------------------------------------------
builder.Services.AddCors(options =>
{
    var corsOptions = builder.Configuration.GetSection("Cors").Get<CorsOptions>()
                      ?? new CorsOptions();

    options.AddPolicy("DefaultCorsPolicy", policy =>
    {
        policy.WithOrigins(corsOptions.AllowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ---------------------------------------------
// Build the app
// ---------------------------------------------
var app = builder.Build();

// ---------------------------------------------
// Configure the HTTP request pipeline
// ---------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    // Keep the token across page reloads so developers don't re-authorize every time
    app.UseSwaggerUI(options => options.ConfigObject.PersistAuthorization = true);
}

app.UseForwardedHeaders();
app.UseRequestLocalization(app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseCors("DefaultCorsPolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<LocationAccessMiddleware>();
app.MapControllers();
app.Run();

public partial class Program { }
