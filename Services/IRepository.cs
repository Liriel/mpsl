using System.Linq;
using Microsoft.AspNetCore.Identity;
using mps.Model;

namespace mps.Services
{
    public interface IRepository
    {
        IQueryable<ShoppingList> ShoppingLists { get; }
        IQueryable<ShoppingListItem> ShoppingListItems { get; }
        IQueryable<Unit> Units { get; }
        IQueryable<ItemHistory> ItemHistories { get; }
        IQueryable<ApplicationUser> Users { get; }
        IQueryable<IdentityRole> Roles { get; }
        IQueryable<IdentityUserRole<string>> UserRoles { get; }

        T Find<T>(int id) where T : EntityBase;

        void Add<T>(T entity) where T : EntityBase;

        int GetUserCount();

        void Remove<T>(T entity) where T : EntityBase;

        void SaveChanges();

        IQueryable<T> GetEntities<T>() where T : EntityBase;
    }
}