using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;
using RestaurantManagement.Api.Data;
using RestaurantManagement.Api.Entities.Organizations;
using RestaurantManagement.Api.Entities.Users;
using RestaurantManagement.Api.Models.Organizations;
using RestaurantManagement.Api.Services.Organizations;
using RestaurantManagement.Api.Utils.Exceptions;
using RestaurantManagement.Api;

namespace RestaurantManagement.Tests.Controller.Organizations
{
    public class OrganizationServiceTests
    {
        private readonly RestaurantDbContext _db;
        private readonly Mock<IStringLocalizer<SharedResource>> _localizerMock;
        private readonly OrganizationService _service;

        public OrganizationServiceTests()
        {
            var options = new DbContextOptionsBuilder<RestaurantDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _db = new RestaurantDbContext(options);
            _localizerMock = new Mock<IStringLocalizer<SharedResource>>();

            // Default localization values
            _localizerMock.Setup(l => l["UserNotFound"]).Returns(new LocalizedString("UserNotFound", "User not found."));
            _localizerMock.Setup(l => l["TrialOrganizationsCannotCreateNew"]).Returns(new LocalizedString("TrialOrganizationsCannotCreateNew", "Trial organizations cannot create new ones."));
            _localizerMock.Setup(l => l["OrganizationNotFound"]).Returns(new LocalizedString("OrganizationNotFound", "Organization not found."));
            _localizerMock.Setup(l => l["OrganizationSettingsNotFound"]).Returns(new LocalizedString("OrganizationSettingsNotFound", "Organization settings not found."));

            _service = new OrganizationService(_db, _localizerMock.Object);
        }

        [Fact]
        public async Task CreateOrganizationAsync_ShouldCreateOrganization_WhenUserExistsAndHasNoTrial()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "owner@test.com",
                FirstName = "John",
                LastName = "Doe",
                PasswordHash = "hashedpassword123"
            };
            await _db.Users.AddAsync(user);
            await _db.SaveChangesAsync();

            var request = new CreateOrganizationRequest
            {
                Name = "Test Org",
                Description = "Test description",
                LogoUrl = "logo.png",
                PrimaryColor = "#000",
                SecondaryColor = "#FFF",
                AccentColor = "#CCC",
                LocationName = "Main Location",
                Address = "123 Main St",
                City = "Barrie",
                State = "ON",
                PostalCode = "L4M 0A1",
                Country = "Canada",
                PhoneNumber = "555-555-5555"
            };

            // Act
            var result = await _service.CreateOrganizationAsync(user.Id, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test Org", result.Name);
            Assert.Equal("TRIAL", result.PlanType);
            Assert.True(result.IsTrialActive);
        }

        [Fact]
        public async Task CreateOrganizationAsync_ShouldThrow_WhenUserNotFound()
        {
            // Arrange
            var request = new CreateOrganizationRequest { Name = "Invalid User Org" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.CreateOrganizationAsync(Guid.NewGuid(), request));

            Assert.Equal("User not found.", ex.Message);
        }

        [Fact]
        public async Task EditOrganizationAsync_ShouldThrow_WhenOrganizationNotFound()
        {
            // Arrange
            var request = new CreateOrganizationRequest { Name = "Edited Org" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.EditOrganizationAsync(Guid.NewGuid(), request));

            Assert.Equal("Organization not found.", ex.Message);
        }

        [Fact]
        public async Task EditOrganizationAsync_ShouldThrow_WhenOrganizationSettingsNotFound()
        {
            // Arrange
            var org = new Organization
            {
                Id = Guid.NewGuid(),
                Name = "Org Without Settings"
            };
            await _db.Organizations.AddAsync(org);
            await _db.SaveChangesAsync();

            var request = new CreateOrganizationRequest { Name = "Updated Name" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.EditOrganizationAsync(org.Id, request));

            Assert.Equal("Organization settings not found.", ex.Message);
        }

        [Fact]
        public async Task EditOrganizationAsync_ShouldUpdateOrganization_WhenValid()
        {
            // Arrange
            var org = new Organization
            {
                Id = Guid.NewGuid(),
                Name = "Old Name"
            };
            var settings = new OrganizationSettings
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                PlanType = "TRIAL",
                IsTrialActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _db.Organizations.AddAsync(org);
            await _db.OrganizationSettings.AddAsync(settings);
            await _db.SaveChangesAsync();

            var request = new CreateOrganizationRequest
            {
                Name = "New Name",
                Description = "Updated Description",
                PrimaryColor = "#111",
                SecondaryColor = "#222",
                AccentColor = "#333"
            };

            // Act
            var result = await _service.EditOrganizationAsync(org.Id, request);

            // Assert
            Assert.Equal("New Name", result.Name);
            Assert.Equal("TRIAL", result.PlanType);
            Assert.Equal("#111", result.PrimaryColor);
        }
    }
}
