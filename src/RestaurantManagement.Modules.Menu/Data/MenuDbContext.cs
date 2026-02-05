using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Menu.Entities;

namespace RestaurantManagement.Modules.Menu.Data
{
    public class MenuDbContext : DbContext
    {
        public MenuDbContext(DbContextOptions<MenuDbContext> options)
            : base(options) {}

        public DbSet<Entities.Menu> Menus { get; set; }
        public DbSet<LocationMenu> LocationMenus { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Apply any entity configurations from this assembly
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(MenuDbContext).Assembly);
            base.OnModelCreating(modelBuilder);
        }
    }
}
