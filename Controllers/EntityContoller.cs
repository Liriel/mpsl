
using System;
using System.Linq;
using System.Linq.Expressions;
using AutoMapper;
using mps.Infrastructure;
using mps.Model;
using mps.Services;
using mps.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;

namespace mps.Controllers
{
    /// <summary>
    /// Specialized controller that handles CRUD operations for a specific entity.
    /// </summary>
    /// <typeparam name="TEntity">Database entity class</typeparam>
    /// <typeparam name="TEditViewModel">Edit view model</typeparam>
    /// <typeparam name="TListViewModel">List view model</typeparam>
    /// <remarks>
    /// This is the part that will acutally call <see="IRepository.SaveChanges" /> after all type
    /// specific services have done their thing.
    /// </remarks>
    [Route("api/[controller]")]
    [Authorize(Policy = "ApprovedUser")]
    [ApiController]
    public class EntityController<TEntity, TEditViewModel, TListViewModel> : Controller
        where TEntity : EntityBase
        where TEditViewModel : EditViewModel, new()
    {
        private readonly IRepository repo;
        private readonly ILogger<EntityController<TEntity, TEditViewModel, TListViewModel>> logger;
        private readonly IMapper mapper;
        private readonly IEntityService<TEntity> svc;

        public EntityController(IEntityService<TEntity> svc, IRepository repo, ILogger<EntityController<TEntity, TEditViewModel, TListViewModel>> logger, IMapper mapper)
        {
            this.svc = svc;
            this.repo = repo;
            this.logger = logger;
            this.mapper = mapper;
        }

        [HttpGet(Order = 1)]
        public virtual IActionResult Index(int skip = 0, int take = 100, string sort = null, SortDirection sortDirection = SortDirection.asc)
        {
            var q = this.ApplySort(this.ApplyAccessFilter(this.repo.GetEntities<TEntity>()), sort, sortDirection);
            return PageAndProjectResult(q, skip, take);
        }

        /// <summary>
        /// Narrows an entity query down to the rows the current caller may see. Defaults to no-op;
        /// override in a derived controller to enforce access (see <see cref="ShoppingListController"/>).
        /// </summary>
        protected virtual IQueryable<TEntity> ApplyAccessFilter(IQueryable<TEntity> query) => query;

        /// <summary>
        /// Determines whether the current caller may act on a single already-loaded entity. Defaults
        /// to allow; override in a derived controller to enforce access.
        /// </summary>
        protected virtual bool CanAccess(TEntity entity) => true;

        internal IActionResult PageAndProjectResult(IQueryable<TEntity> query, int skip = 0, int take = 100)
        {
            int totalRecordCount = query.Count();
            take = Math.Min(take, Constants.MAX_LIST_ROWS);
            return Json(new ListResult<TListViewModel>
            {
                TotalRecordCount = totalRecordCount,
                Results = this.mapper.ProjectTo<TListViewModel>(query.Skip(skip).Take(take))
            });
        }

        internal IQueryable<TEntity> ApplySort(IQueryable<TEntity> query, string sort = null, SortDirection sortDirection = SortDirection.asc)
        {

            // apply sort if specified
            if (!string.IsNullOrWhiteSpace(sort))
            {
                // fix json camel case
                sort = sort.Substring(0, 1).ToUpper() + sort.Substring(1);

                var sortFunc = this.GetPropertyFunc(sort);
                if (sortFunc == null)
                    return query;

                if (sortDirection == SortDirection.asc)
                    query = query.OrderBy(sortFunc);
                else
                    query = query.OrderByDescending(sortFunc);
            }

            return query;
        }

        [HttpGet("{id}")]
        public IActionResult Get(int id)
        {
            var entity = this.repo.Find<TEntity>(id);
            if (entity == null)
                return new NotFoundObjectResult($"{typeof(TEntity).Name} with id {id} not found.");

            if (!this.CanAccess(entity))
                return Forbid();

            var viewModel = this.mapper.ProjectTo<TEditViewModel>(this.ApplyAccessFilter(this.repo.GetEntities<TEntity>()).Where(s => s.Id == id)).SingleOrDefault();
            return new OkObjectResult(viewModel);
        }

        [HttpPost(Order = 1)]
        public IActionResult Post(TEditViewModel viewModel)
        {
            WebOperationResult<TEditViewModel, TEntity> result;
            if (ModelState.IsValid)
            {
                if (viewModel.Id != 0)
                    return new BadRequestObjectResult($"{typeof(TEntity).Name} has already been added");

                var entity = this.mapper.Map<TEntity>(viewModel);
                this.PostMapping(viewModel, entity);

                var validationResult = this.svc.Validate(entity);
                if (!validationResult.Success)
                    return new BadRequestObjectResult(validationResult);

                var saveResult = this.svc.AddOrUpdate(entity);
                result = new WebOperationResult<TEditViewModel, TEntity>(mapper, saveResult);
            }
            else
            {
                result = new WebOperationResult<TEditViewModel, TEntity>(ModelState);
            }

            return new OkObjectResult(result);
        }

        [HttpPut("{id}", Order = 1)]
        public IActionResult Put(int id, TEditViewModel viewModel)
        {
            WebOperationResult<TEditViewModel, TEntity> result;
            if (ModelState.IsValid)
            {
                if (id == 0)
                    return new BadRequestObjectResult("Use POST to create new values");

                var entity = this.repo.Find<TEntity>(id);
                if (entity == null)
                    return new NotFoundObjectResult($"{typeof(TEntity).Name} with id {id} not found.");

                if (!this.CanAccess(entity))
                    return Forbid();

                this.mapper.Map<TEditViewModel, TEntity>(viewModel, entity);
                this.PostMapping(viewModel, entity);

                var validationResult = this.svc.Validate(entity);
                if (!validationResult.Success)
                    return new BadRequestObjectResult(validationResult);

                var saveResult = this.svc.AddOrUpdate(entity);
                result = new WebOperationResult<TEditViewModel, TEntity>(mapper, saveResult);
            }
            else
            {
                result = new WebOperationResult<TEditViewModel, TEntity>(ModelState);
            }

            return new OkObjectResult(result);
        }

        protected virtual void PostMapping(TEditViewModel viewModel, TEntity entity)
        { }

        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            var entity = this.repo.Find<TEntity>(id);
            if (entity == null)
                return new NotFoundObjectResult($"{typeof(TEntity).Name} with id {id} not found.");

            if (!this.CanAccess(entity))
                return Forbid();

            // TODO: implement delete callback in entity service
            this.repo.Remove(entity);
            this.repo.SaveChanges();
            return new OkResult();
        }

        private Expression<Func<TEntity, object>> GetPropertyFunc(string propertyName)
        {
            var type = typeof(TEntity);
            var property = type.GetProperty(propertyName);
            if (property == null)
                return null;

            var parameter = Expression.Parameter(type);
            var access = Expression.Property(parameter, property);
            var convert = Expression.Convert(access, typeof(object));
            return Expression.Lambda<Func<TEntity, object>>(convert, parameter);
        }
    }
}