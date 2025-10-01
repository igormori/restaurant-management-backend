using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Api.Entities.Users;

namespace RestaurantManagement.Api.Data
{
    public class RestaurantDbContext : DbContext
    {
        public RestaurantDbContext(DbContextOptions<RestaurantDbContext> options) 
        : base(options) {}

        public DbSet<User> Users { get; set; } 

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(u => u.Id);

                entity.HasMany(u => u.Roles)
                    .WithOne(r => r.User)
                    .HasForeignKey(r => r.UserId);
            });

            modelBuilder.Entity<UserRole>(entity =>
            {
                entity.ToTable("user_roles");
                entity.HasKey(r => r.Id);
            });
        }
    }
}