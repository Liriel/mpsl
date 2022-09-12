using System.Collections.Generic;

namespace mps.Infrastructure
{
    public class ValidationError
    {
        public string Message { get; private set; }

        public List<string> Fields { get; private set; }

        public ValidationError(string message, params string[] fields)
        {
            this.Fields = new List<string>();
            this.Fields.AddRange(fields);
            this.Message = message;
        }
    }
}