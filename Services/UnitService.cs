using mps.Infrastructure;
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

    public EntityOperationResult<Unit> GetOrCreateUnit(string unitShortName)
    {
        // check if the unit exists or should be created
        if (string.IsNullOrWhiteSpace(unitShortName))
            return new EntityOperationResult<Unit> { Success = true };

        unitShortName = unitShortName.Trim();

        // ShortName is unique, but only case sensitively - a case insensitive lookup can therefore
        // still match several rows, so take the oldest instead of throwing.
        var unit = this.repo.Units
            .Where(u => u.ShortName.ToLower() == unitShortName.ToLower())
            .OrderBy(u => u.Id)
            .FirstOrDefault();

        if (unit == null)
        {
            if (!UnitParser.IsPlausibleUnit(unitShortName))
                return new EntityOperationResult<Unit>(
                    new ValidationError("A unit needs at least one letter", "UnitShortName"));

            // persist immediately so callers get a unit with a real Id they can assign to a
            // foreign key - returning a detached Id = 0 unit silently wrote UnitId = 0 before
            unit = new Unit { ShortName = unitShortName, Name = unitShortName };
            this.repo.Add(unit);
            this.repo.SaveChanges();
            this.logger.LogInformation("Created unit {ShortName} with id {Id}", unit.ShortName, unit.Id);
        }

        return new EntityOperationResult<Unit> { Success = true, Entity = unit };
    }
}
