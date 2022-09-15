using mps.Model;

namespace mps.Services;

public interface IUnitService
{
    Unit GetOrCreateUnit(string unitShortName);
}
