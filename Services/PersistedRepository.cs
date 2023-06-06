using System;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using mps.Infrastructure;
using mps.Model;

namespace mps.Services
{
    public class PersistedRepository : IRepository
    {
        private readonly DataContext context;
        private readonly IUserIdentityService identityService;

        public PersistedRepository(DataContext context, IUserIdentityService identityService)
        {
            this.context = context;
            this.identityService = identityService;
        }

        public IQueryable<ShoppingList> ShoppingLists => this.context.ShoppingLists;
        public IQueryable<ShoppingListItem> ShoppingListItems => this.context.ShoppingListItems;
        public IQueryable<Unit> Units => this.context.Units;
        public IQueryable<ItemHistory> ItemHistories => this.context.ItemHistories;
        public IQueryable<ApplicationUser> Users => this.context.Users;
        public IQueryable<IdentityRole> Roles => this.context.Roles;
        public IQueryable<IdentityUserRole<string>> UserRoles => this.context.UserRoles;

        public void SaveChanges()
        {
            // Updating the timestamp/user fields should probably be done in the service rather then the repo itself
            // Therefore the repo should expose the change information. But since we don't want to tie the service to EF
            // the functionality stays here for now.
            var currentUser = this.GetUserByName(this.identityService.CurrentUserName);
            if (currentUser == null)
            {
                // HACK: fallback to system user
                currentUser = this.Users.Single(u => u.Id == Constants.SYSUSER_ID);
            }

            foreach (EntityEntry dbEntityEntry in this.context.ChangeTracker.Entries())
            {
                var entity = dbEntityEntry.Entity as EntityBase;
                if (entity == null)
                {
                    continue;
                }

                if (dbEntityEntry.State == EntityState.Added)
                {
                    // TODO: uncomment or delete
                    // entity.CreatedBy = currentUser;
                    // entity.CreationDate = DateTime.Now;
                }

                if ((dbEntityEntry.State == EntityState.Modified) || (dbEntityEntry.State == EntityState.Added))
                {
                    // TODO: uncomment or delete
                    // entity.LastChangedBy = currentUser;
                    // entity.LastChangedDate = DateTime.Now;
                }
            }

            this.context.SaveChanges();
        }

        public void Add<T>(T entity) where T : EntityBase
        {
            this.context.Add<T>(entity);
        }

        public IQueryable<T> GetEntities<T>() where T : EntityBase => this.context.Set<T>().AsQueryable();

        public int GetUserCount()
        {
            return this.context.Users.Where(u => u.Id != Constants.SYSUSER_ID).Count();
        }

        public void Remove<T>(T entity) where T : EntityBase
        {
            this.context.Remove<T>(entity);
        }

        public T Find<T>(int id) where T : EntityBase
        {
            var result = this.context.Find(typeof(T), id);
            if (result == null)
                return default(T);
            else
                return (T)result;
        }

        private ApplicationUser GetUserByName(string userName)
        {
            return this.Users.SingleOrDefault(u => u.UserName == userName);
        }

    }
}