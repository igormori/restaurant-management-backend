using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Identity.Data;
using Testcontainers.PostgreSql;
using Xunit;

namespace RestaurantManagement.Modules.Identity.Tests
{
    /// <summary>
    /// Starts a real Postgres container and applies every Identity migration against it, so
    /// tests can exercise database-level behaviour (constraints, concurrency) that the SQLite
    /// in-memory database used by other tests cannot prove.
    /// </summary>
    public class IdentityPostgresFixture : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();

        public async Task InitializeAsync()
        {
            await _container.StartAsync();

            using var context = CreateContext();
            await context.Database.MigrateAsync();
        }

        public IdentityDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<IdentityDbContext>()
                .UseNpgsql(_container.GetConnectionString())
                .UseSnakeCaseNamingConvention()
                .Options;

            return new IdentityDbContext(options);
        }

        public Task DisposeAsync() => _container.DisposeAsync().AsTask();
    }
}
