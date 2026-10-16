using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RestaurantManagement.Modules.Menu.Data;
using RestaurantManagement.Shared.Data;

namespace RestaurantManagement.Modules.Menu.Tests
{
    /// <summary>
    /// Opens a SQLite in-memory connection and builds a MenuDbContext against it.
    /// Kept open for the lifetime of the instance so the in-memory database survives
    /// across multiple DbContext usages within a single test.
    /// </summary>
    public class MenuTestDatabase : IDisposable
    {
        private readonly SqliteConnection _connection;

        public MenuTestDatabase()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            using var context = CreateContext();
            context.Database.EnsureCreated();
        }

        /// <summary>
        /// A context with no organization: use it to seed rows. Its reads see no tenant rows once
        /// the query filters apply; read back with a tenant or with IgnoreQueryFilters().
        /// </summary>
        public MenuDbContext CreateContext() => CreateContext(TestTenant.None);

        public MenuDbContext CreateContext(IInterceptor interceptor) => CreateContext(TestTenant.None, interceptor);

        public MenuDbContext CreateContext(ITenantContext tenantContext, IInterceptor? interceptor = null)
        {
            var builder = new DbContextOptionsBuilder<MenuDbContext>().UseSqlite(_connection);
            if (interceptor is not null)
                builder.AddInterceptors(interceptor);

            return new MenuDbContext(builder.Options, tenantContext);
        }

        /// <summary>
        /// Closes the underlying connection so a subsequent operation against it (e.g. a
        /// transaction rollback) fails, simulating an unavailable database.
        /// </summary>
        public void CloseConnection()
        {
            _connection.Close();
        }

        public void Dispose()
        {
            _connection.Dispose();
        }
    }
}
