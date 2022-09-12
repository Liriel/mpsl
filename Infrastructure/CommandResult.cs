using System.Collections.Generic;

namespace mps.Infrastructure
{
    public class OperationResult
    {
        public bool Success { get; set; }
        public List<string> Messages { get; set; }

        public static OperationResult Error(params string[] messages){
            var result = new OperationResult{Success = false};
            result.Messages.AddRange(messages);
            return result;
        }
        public static OperationResult Ok(params string[] messages){
            var result = new OperationResult{Success = true};
            result.Messages.AddRange(messages);
            return result;
        }

        public OperationResult(){
            this.Success = true;
            this.Messages = new List<string>();
        }
    }
}