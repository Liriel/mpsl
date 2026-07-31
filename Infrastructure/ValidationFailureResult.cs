using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace mps.Infrastructure
{
    /// <summary>
    /// The <see cref="OperationResult{T}"/> JSON shape without an entity, for write failures raised
    /// before a controller action runs.
    /// </summary>
    /// <remarks>
    /// <c>[ApiController]</c> short-circuits an invalid <see cref="ModelStateDictionary"/> in a
    /// filter, so the action's own ModelState branch never executes and the framework would answer
    /// with an RFC 7807 ProblemDetails body instead. Wiring
    /// <c>ApiBehaviorOptions.InvalidModelStateResponseFactory</c> to this type keeps every 400 from
    /// the API in one shape - <c>{ success, entity, validationErrors }</c> - that the Angular
    /// <c>EntityOperationResult</c> can read, no matter which layer rejected the write.
    /// </remarks>
    public class ValidationFailureResult
    {
        public bool Success => false;

        public object Entity => null;

        public List<ValidationError> ValidationErrors { get; set; } = new List<ValidationError>();

        public static ValidationFailureResult FromModelState(ModelStateDictionary modelState)
        {
            var result = new ValidationFailureResult();
            foreach (var entry in modelState)
            {
                foreach (var error in entry.Value.Errors)
                {
                    var message = string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "The value is invalid."
                        : error.ErrorMessage;

                    result.ValidationErrors.Add(new ValidationError(message, entry.Key));
                }
            }

            return result;
        }
    }
}
