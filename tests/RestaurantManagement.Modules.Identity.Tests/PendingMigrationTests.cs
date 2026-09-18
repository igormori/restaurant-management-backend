using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Identity.Services;
using RestaurantManagement.Shared.Services.Email;
using Testcontainers.PostgreSql;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// Reproduces the production 500 seen when a database has been migrated with an older
    /// migration history that predates a later Identity migration: RegisterAsync fails because
    /// the model expects columns (verification_failed_attempts, verification_locked_until)
    /// that the database does not yet have.
    /// </summary>
    public class PendingMigrationTests
    {
        [Fact]
        public async Task RegisterAsync_DatabaseMissingAddVerificationAttemptTrackingMigration_ThrowsDbUpdateException()
        {
            // Arrange: a database migrated only up to InitialCreate, one migration behind
            // the current model (AddVerificationAttemptTracking has not been applied).
            await using var container = new PostgreSqlBuilder("postgres:16-alpine").Build();
            await container.StartAsync();

            var options = new DbContextOptionsBuilder<Data.IdentityDbContext>()
                .UseNpgsql(container.GetConnectionString())
                .UseSnakeCaseNamingConvention()
                .Options;

            await using (var migrationContext = new Data.IdentityDbContext(options))
            {
                var migrator = migrationContext.GetService<IMigrator>();
                await migrator.MigrateAsync("20260203053311_InitialCreate");
            }

            var emailService = Substitute.For<IEmailService>();
            var logger = Substitute.For<ILogger<RegistrationService>>();

            await using var context = new Data.IdentityDbContext(options);
            var sut = new RegistrationService(context, TestSecurityOptions.Create(), TestLocalizer.Create(), emailService, logger);

            var request = new RegisterRequest
            {
                Email = "newuser@test.com",
                Password = "Password1!",
                FirstName = "New",
                LastName = "User"
            };

            // Act
            var act = () => sut.RegisterAsync(request);

            // Assert: matches the "column verification_failed_attempts does not exist" error
            // reported when registering against an under-migrated database.
            await act.Should().ThrowAsync<DbUpdateException>();
        }

        [Fact]
        public async Task RegisterAsync_DatabaseFullyMigrated_Succeeds()
        {
            // Arrange: same starting point as above, but with every migration applied.
            await using var container = new PostgreSqlBuilder("postgres:16-alpine").Build();
            await container.StartAsync();

            var options = new DbContextOptionsBuilder<Data.IdentityDbContext>()
                .UseNpgsql(container.GetConnectionString())
                .UseSnakeCaseNamingConvention()
                .Options;

            await using (var migrationContext = new Data.IdentityDbContext(options))
            {
                await migrationContext.Database.MigrateAsync();
            }

            var emailService = Substitute.For<IEmailService>();
            var logger = Substitute.For<ILogger<RegistrationService>>();

            await using var context = new Data.IdentityDbContext(options);
            var sut = new RegistrationService(context, TestSecurityOptions.Create(), TestLocalizer.Create(), emailService, logger);

            var request = new RegisterRequest
            {
                Email = "newuser@test.com",
                Password = "Password1!",
                FirstName = "New",
                LastName = "User"
            };

            // Act
            var act = () => sut.RegisterAsync(request);

            // Assert
            await act.Should().NotThrowAsync();
        }
    }
}
