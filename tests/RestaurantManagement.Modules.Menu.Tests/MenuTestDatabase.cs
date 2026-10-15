using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RestaurantManagement.Modules.Menu.Data;

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

        public MenuDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<MenuDbContext>()
                .UseSqlite(_connection)
                .Options;

            return new MenuDbContext(options);
        }

        public MenuDbContext CreateContext(IInterceptor interceptor)
        {
            var options = new DbContextOptionsBuilder<MenuDbContext>()
                .UseSqlite(_connection)
                .AddInterceptors(interceptor)
                .Options;

            return new MenuDbContext(options);
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
