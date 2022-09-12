using System;
using System.IO;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace mps.Model
{

    public class DataContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
    {

        public DbSet<ShoppingList> ShoppingLists { get; set; }
        public DbSet<ShoppingListItem> ShoppingListItems { get; set; }
        public DbSet<Unit> Units { get; set; }
        public DbSet<ItemHistory> ItemHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // https://stackoverflow.com/a/34013431/1859022
            base.OnModelCreating(modelBuilder);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!Directory.Exists("data"))
                Directory.CreateDirectory("data");

            string dbPath = Path.Combine("data", "mps.db");
            optionsBuilder.UseLazyLoadingProxies();
            optionsBuilder.UseSqlite("Data Source=" + dbPath);
        }
    }
}
