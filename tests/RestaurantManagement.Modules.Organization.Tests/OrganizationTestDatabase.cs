using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Organization.Data;

namespace RestaurantManagement.Modules.Organization.Tests
{
    /// <summary>
    /// Opens a SQLite in-memory connection and builds an OrganizationDbContext against it.
    /// Kept open for the lifetime of the instance so the in-memory database survives
    /// across multiple DbContext usages within a single test.
    /// </summary>
    public class OrganizationTestDatabase : IDisposable
    {
        private readonly SqliteConnection _connection;

        public OrganizationTestDatabase()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            using var context = CreateContext();
            context.Database.EnsureCreated();
        }

        public OrganizationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<OrganizationDbContext>()
                .UseSqlite(_connection)
                .Options;

            return new OrganizationDbContext(options);
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
