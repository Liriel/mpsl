using System.Collections.Generic;

namespace mps.Infrastructure
{
    public abstract class OperationResult<T> where T: class 
    {
        public bool Success { get; set; }
        public T Entity { get; set; }
        public List<ValidationError> ValidationErrors { get; set; }

        public OperationResult()
        {
            this.ValidationErrors = new List<ValidationError>();
            this.Success = true;
        }

        public OperationResult(params ValidationError[] validationErrors)
            : this()
        {
            this.ValidationErrors.AddRange(validationErrors);
            this.Success = false;
        }
    }
}