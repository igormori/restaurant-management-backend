using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Api.Data;

namespace RestaurantManagement.Tests.Shared
{
    public static class TestDbContextFactory
    {
        public static RestaurantDbContext CreateInMemoryDb()
        {
            var options = new DbContextOptionsBuilder<RestaurantDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new RestaurantDbContext(options);
        }
    }
}