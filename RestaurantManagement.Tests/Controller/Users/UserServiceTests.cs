using Xunit;
using RestaurantManagement.Api.Services.Users;
using RestaurantManagement.Api.Models.Users;
using RestaurantManagement.Tests.Shared;

namespace RestaurantManagement.Tests.Users
{
    public class UserServiceTests
    {
        [Fact]
        public async Task RegisterAsync_Should_Create_User_When_Email_Is_New()
        {
            // Arrange
            var context = TestDbContextFactory.CreateInMemoryDb();
            var service = new UserService(context);

            var request = new RegisterRequest
            {
                Email = "test@example.com",
                Password = "MyPassword123",
                FirstName = "John",
                LastName = "Doe"
            };

            // Act
            var result = await service.RegisterAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("test@example.com", result.Email);
        }
    }
}