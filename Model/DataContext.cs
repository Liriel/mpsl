using System;
using System.IO;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using mps.Infrastructure;

namespace mps.Model
{

    public class DataContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
    {
        // parameterless: production path, falls back to the hardcoded data/mps.db below.
        public DataContext() { }

        // lets callers (tests, or any future config-driven connection string) supply options via
        // DI - OnConfiguring below leaves them alone when this constructor was used.
        public DataContext(DbContextOptions<DataContext> options) : base(options) { }

        public DbSet<ShoppingList> ShoppingLists { get; set; }
        public DbSet<ShoppingListItem> ShoppingListItems { get; set; }
        public DbSet<Unit> Units { get; set; }
        public DbSet<Recommendation> Recommendations { get; set; }
        public DbSet<ItemHistory> ItemHistories { get; set; }
        public DbSet<PushSubscription> PushSubscriptions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // https://stackoverflow.com/a/34013431/1859022
            base.OnModelCreating(modelBuilder);

            // system user
            modelBuilder.Entity<ApplicationUser>().HasData(new[]{
                new ApplicationUser{ Id = Constants.SYSUSER_ID, UserName = "SYSTEM", LockoutEnabled = true }
            });

            // units
            modelBuilder.Entity<Unit>().HasData(new[]{
                new Unit { Id = 1, ShortName = "Stk", Name = "Stück" },
                new Unit { Id = 2, ShortName = "g", Name = "Gramm" },
                new Unit { Id = 3, ShortName = "kg", Name = "Kilo" },
                new Unit { Id = 4, ShortName = "l", Name = "Liter" },
            });

            modelBuilder.Entity<Recommendation>()
                .HasNoKey()
                .ToView("Recommendations");
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // tests (and any future config-driven connection string) configure the context
            // themselves via AddDbContext/UseSqlite before it reaches here - don't override that.
            if (!optionsBuilder.IsConfigured)
            {
                if (!Directory.Exists("data"))
                    Directory.CreateDirectory("data");

                string dbPath = Path.Combine("data", "mps.db");
                optionsBuilder.UseLazyLoadingProxies();
                optionsBuilder.UseSqlite("Data Source=" + dbPath);
            }
        }
    }
}
