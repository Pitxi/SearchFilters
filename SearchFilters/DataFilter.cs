using System.Collections.Generic;

namespace SearchFilters;

public class DataFilter<TValue>
{
    public string              ComparisonName { get; set; } = null!;
    public IEnumerable<TValue> Values         { get; set; } = null!;
}
