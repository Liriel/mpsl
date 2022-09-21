using mps.Model;
using mps.Services;

namespace mps.Services;

public class UnitService : IUnitService
{
    private readonly IRepository repo;
    private readonly ILogger<UnitService> logger;

    public UnitService(IRepository repo, ILogger<UnitService> logger)
    {
        this.logger = logger;
        this.repo = repo;
    }

    public Unit GetOrCreateUnit(string unitShortName)
    {
        // check if the unit exists or should be created
        if (string.IsNullOrWhiteSpace(unitShortName))
            return null;

        var unit = this.repo.Units.SingleOrDefault(u => u.ShortName.ToLower() == unitShortName.ToLower());
        if (unit == null)
        {
            unit = new Unit { ShortName = unitShortName };
        }
        return unit;
    }
}
