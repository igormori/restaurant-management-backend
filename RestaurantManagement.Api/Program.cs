// ---------------------------------------------
// Using directives
// ---------------------------------------------
using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Api.Data;
using RestaurantManagement.Api.Services.Auth;
using RestaurantManagement.Api.Options;
using RestaurantManagement.Api.Services.Organizations;
using Sentry;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

// ---------------------------------------------
// Create WebApplication builder
// ---------------------------------------------
var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------
// Add services to the container
// ---------------------------------------------

// ---------------------------------------------
// JWT Authentication
// ---------------------------------------------
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

        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };
});

// Add controller support (API endpoints)
builder.Services.AddControllers();

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

// Global Exception Handling Middleware
app.UseMiddleware<RestaurantManagement.Api.Middleware.ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();
app.UseCors("DefaultCorsPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();