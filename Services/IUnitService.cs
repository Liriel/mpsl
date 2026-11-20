using mps.Infrastructure;
using mps.Model;

namespace mps.Services;

public interface IUnitService
{
    /// <summary>
    /// Finds or creates the unit. A null/blank name succeeds with a null entity (no unit); a new
    /// short name without any letter fails with a validation error on <c>UnitShortName</c>.
    /// </summary>
    EntityOperationResult<Unit> GetOrCreateUnit(string unitShortName);
}
