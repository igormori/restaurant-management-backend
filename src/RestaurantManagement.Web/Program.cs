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
using RestaurantManagement.Shared.Middleware;
using RestaurantManagement.Shared.Options;
using RestaurantManagement.Shared.Services.Email;
using RestaurantManagement.Shared.Utils.Localization;

// Modules
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Modules.Menu.Data;
using RestaurantManagement.Modules.Menu.Services;
using RestaurantManagement.Modules.Organization.Data;
using RestaurantManagement.Modules.Organization.Services;

var builder = WebApplication.CreateBuilder(args);

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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key))
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
    .AddApplicationPart(typeof(RestaurantManagement.Modules.Identity.Controllers.AuthController).Assembly)
    .AddApplicationPart(typeof(RestaurantManagement.Modules.Menu.Controllers.MenuController).Assembly)
    .AddApplicationPart(typeof(RestaurantManagement.Modules.Organization.Controllers.OrganizationController).Assembly);

// ---------------------------------------------
// Options Configuration
// ---------------------------------------------
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Security"));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<CorsOptions>(builder.Configuration.GetSection("Cors"));

// ---------------------------------------------
// Database Contexts (Per Module)
// ---------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

builder.Services.AddDbContext<MenuDbContext>(options =>
    options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

builder.Services.AddDbContext<OrganizationDbContext>(options =>
    options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

// ---------------------------------------------
// Sentry for error tracking
// ---------------------------------------------
builder.WebHost.UseSentry(o =>
{
    o.Dsn = builder.Configuration["Sentry:Dsn"];
    o.Debug = true;
    o.TracesSampleRate = 1.0;
});

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
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
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
builder.Services.AddScoped<IVerificationService, VerificationService>();

// Organization Module Services
builder.Services.AddScoped<IOrganizationService, OrganizationService>();
builder.Services.AddScoped<ILocationService, LocationService>();

// Menu Module Services
builder.Services.AddScoped<RestaurantManagement.Modules.Menu.Services.IMenuService, RestaurantManagement.Modules.Menu.Services.MenuService>();

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
              .AllowAnyMethod();
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
    app.UseSwaggerUI();
}

app.UseRequestLocalization(app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseCors("DefaultCorsPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
