using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Api.Data;
using RestaurantManagement.Api.Services.Auth;
using Sentry;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();


// JWT Auth
var jwtKey = builder.Configuration["Jwt:Key"] ?? "super_secret_dev_key";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "RestaurantApp";

// EF Core + Npgsql + snake_case
builder.Services.AddDbContext<RestaurantDbContext>(options =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
        .UseSnakeCaseNamingConvention());

// Add Sentry
builder.WebHost.UseSentry(o =>
{
    o.Dsn = builder.Configuration["Sentry:Dsn"]; 
    o.Debug = true; 
    o.TracesSampleRate = 1.0; 
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register the User service
builder.Services.AddScoped<IAuthService, AuthService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Global Exception Handling
app.UseMiddleware<RestaurantManagement.Api.Middleware.ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();
app.UseAuthentication(); 
app.UseAuthorization();
app.MapControllers();
app.Run();