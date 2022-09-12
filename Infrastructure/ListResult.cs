using System.Collections.Generic;

namespace mps.Infrastructure
{
    public class ListResult<T>
    {
        public int TotalRecordCount { get; set; }
        public IEnumerable<T> Results { get; set; }
    }
}