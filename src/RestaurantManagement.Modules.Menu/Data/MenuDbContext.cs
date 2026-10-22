using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Menu.Entities;
using RestaurantManagement.Shared.Data.Tenancy;

namespace RestaurantManagement.Modules.Menu.Data
{
    public class MenuDbContext : DbContext
    {
        private readonly ITenantContext _tenantContext;

        public MenuDbContext(DbContextOptions<MenuDbContext> options, ITenantContext tenantContext)
            : base(options)
        {
            _tenantContext = tenantContext;
        }

        public DbSet<Entities.Menu> Menus { get; set; }
        public DbSet<LocationMenu> LocationMenus { get; set; }
        public DbSet<MenuCategory> MenuCategories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Apply any entity configurations from this assembly
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(MenuDbContext).Assembly);
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyTenantQueryFilters(() => _tenantContext);
        }
    }
}
