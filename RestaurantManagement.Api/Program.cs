// ---------------------------------------------
// Using directives
// ---------------------------------------------
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Localization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;
using Sentry;
using RestaurantManagement.Api.Services.Organizations;
using RestaurantManagement.Api.Utils.Localization;
using RestaurantManagement.Api.Services.Auth;
using RestaurantManagement.Api.Options;
using RestaurantManagement.Api.Data;


// ---------------------------------------------
// Create WebApplication builder
// ---------------------------------------------
var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------
// Add services to the container
// ---------------------------------------------


// ---------------------------------------------
// Add Localization
// ---------------------------------------------
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

Console.WriteLine("Culture: " + CultureInfo.CurrentUICulture);
CultureInfo.CurrentCulture = new CultureInfo("pt");
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

    // Optional: normalize pt-BR and pt-PT to just "pt"
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
        IssuerSigningKey = new SymmetricSecurityKey(
        Encoding.UTF8.GetBytes(jwtOptions.Key))
    };
});

// Add controller support (API endpoints) + enable validation localization
builder.Services.AddControllers()
    .AddDataAnnotationsLocalization(options =>
    {
        options.DataAnnotationLocalizerProvider = (type, factory) =>
            factory.Create(typeof(RestaurantManagement.Api.SharedResource));
    });

// ---------------------------------------------
// Options variables Configuration
// ---------------------------------------------
builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection("Security"));

builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection("Jwt"));

// ---------------------------------------------
// Entity Framework Core + Npgsql + snake_case naming
// ---------------------------------------------
builder.Services.AddDbContext<RestaurantDbContext>(options =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
        .UseSnakeCaseNamingConvention());

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
// Swagger/OpenAPI for API documentation
// ---------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ---------------------------------------------
// Dependency Injection: Register application services
// ---------------------------------------------
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IOrganizationService, OrganizationService>();


// ---------------------------------------------
// Define a CORS policy
// ---------------------------------------------
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCorsPolicy", policy =>
    {
        policy.WithOrigins(
                "https://your-frontend-app.com",
                "http://localhost:3000", 
                "http://localhost:4200"   
            )
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

//Localiation
app.UseRequestLocalization(app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);


// Global Exception Handling Middleware
app.UseMiddleware<RestaurantManagement.Api.Middleware.ExceptionHandlingMiddleware>();


app.UseHttpsRedirection();
app.UseCors("DefaultCorsPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();