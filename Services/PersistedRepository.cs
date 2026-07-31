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
        private string cachedUserName;
        private string cachedUserId;

        public PersistedRepository(DataContext context, IUserIdentityService identityService)
        {
            this.context = context;
            this.identityService = identityService;
        }

        public IQueryable<ShoppingList> ShoppingLists => this.context.ShoppingLists;
        public IQueryable<ShoppingListItem> ShoppingListItems => this.context.ShoppingListItems;
        public IQueryable<Unit> Units => this.context.Units;
        public IQueryable<Recommendation> Recommendations => this.context.Recommendations;
        public IQueryable<ItemHistory> ItemHistories => this.context.ItemHistories;
        public IQueryable<ApplicationUser> Users => this.context.Users;
        public IQueryable<IdentityRole> Roles => this.context.Roles;
        public IQueryable<IdentityUserRole<string>> UserRoles => this.context.UserRoles;

        public void SaveChanges()
        {
            this.StampChangedEntities();
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

        public void Delete<T>(T entity) where T : EntityBase
        {
            this.context.Remove<T>(entity);
        }

        public async Task SaveChangesAsync()
        {
            this.StampChangedEntities();
            await this.context.SaveChangesAsync();
        }

        /// <summary>
        /// Stamps every added or modified <see cref="ITrackLastChange"/> entity with the current
        /// user and time.
        /// </summary>
        /// <remarks>
        /// Updating the timestamp/user fields should probably be done in the service rather than the
        /// repo itself, which would mean exposing the change information from the repo. But since we
        /// don't want to tie the service to EF the functionality stays here for now.
        /// </remarks>
        private void StampChangedEntities()
        {
            var changed = this.context.ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Modified || e.State == EntityState.Added)
                .Select(e => e.Entity as ITrackLastChange)
                .Where(e => e != null)
                .ToList();

            // Resolving the user costs a query, so only pay for it when something needs stamping.
            if (changed.Count == 0)
                return;

            var currentUserId = this.GetCurrentUserId();
            var timestamp = DateTime.UtcNow;

            foreach (var entity in changed)
            {
                entity.LastChangeByUserId = currentUserId;
                entity.LastChangedDate = timestamp;
            }
        }

        /// <summary>
        /// Resolves the id of the user to attribute changes to, falling back to the system user.
        /// Cached for the lifetime of the (scoped) repository so repeated saves in one request do
        /// not re-query the user table.
        /// </summary>
        private string GetCurrentUserId()
        {
            var userName = this.identityService.CurrentUserName;
            if (this.cachedUserId != null && this.cachedUserName == userName)
                return this.cachedUserId;

            var currentUser = this.GetUserByName(userName);
            this.cachedUserId = currentUser?.Id
                ?? this.Users.Single(u => u.Id == Constants.SYSUSER_ID).Id;
            this.cachedUserName = userName;

            return this.cachedUserId;
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