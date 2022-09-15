
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
    [ApiController]
    public class EntityController<TEntity, TEditViewModel, TListViewModel> : Controller
        where TEntity : class, IEntity
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
            var q = this.ApplySort(this.repo.GetEntities<TEntity>(), sort, sortDirection);
            return PageAndProjectResult(q, skip, take);
        }

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
        public TEditViewModel Get(int id)
        {
            return this.mapper.ProjectTo<TEditViewModel>(this.repo.GetEntities<TEntity>().Where(s => s.Id == id)).SingleOrDefault();
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
                if (saveResult.Success)
                    this.repo.SaveChanges();

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

                this.mapper.Map<TEditViewModel, TEntity>(viewModel, entity);
                this.PostMapping(viewModel, entity);

                var validationResult = this.svc.Validate(entity);
                if (!validationResult.Success)
                    return new BadRequestObjectResult(validationResult);

                var saveResult = this.svc.AddOrUpdate(entity);
                if (saveResult.Success)
                    this.repo.SaveChanges();

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