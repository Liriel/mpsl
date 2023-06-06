using AutoMapper;
using mps.Model;
using mps.ViewModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace mps.Infrastructure{

    /// <summary>
    /// Extends the <see="EntityOperationResult" /> with ModelState handling.
    /// </summary>
    public class WebOperationResult<TEditViewModel, TEntity> : OperationResult<TEditViewModel>
        where TEntity : EntityBase
        where TEditViewModel : EditViewModel, new()
    {
        public WebOperationResult()
            : base()
        { }

        public WebOperationResult(IMapper mapper, EntityOperationResult<TEntity> entityOperationResult)
            : base()
        {
            if (!entityOperationResult.Success)
            {
                this.Success = false;
                this.ValidationErrors.AddRange(entityOperationResult.ValidationErrors);
            }

            if (entityOperationResult.Entity != null)
            {
                this.Entity = mapper.Map<TEntity, TEditViewModel>(entityOperationResult.Entity);
            }
        }

        public WebOperationResult(ModelStateDictionary modelState)
            : base()
        {
            if (!modelState.IsValid)
            {
                this.Success = false;
                foreach (var item in modelState)
                {
                    foreach (var error in item.Value.Errors)
                    {
                        this.ValidationErrors.Add(new ValidationError(error.ErrorMessage, item.Key));
                    }
                }
            }
        }
    }
}