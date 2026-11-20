using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using mps.Model;
using mps.Services;
using mps.ViewModels;

namespace mps.Controllers;

[ApiController]
[Authorize(Policy = "ApprovedUser")]
[Route("api/[controller]")]
public class UnitController : EntityController<Unit, UnitEditViewModel, UnitViewModel>
{
    private readonly ILogger<UnitController> logger;
    private readonly IRepository repo;
    private readonly IMapper mapper;

    public UnitController(IEntityService<Unit> entitySvc, IRepository repo, ILogger<UnitController> logger, IMapper mapper)
        : base(entitySvc, repo, logger, mapper)
    {
        this.repo = repo;
        this.logger = logger;
        this.mapper = mapper;
    }

    /// <summary>A unit without a long name is only suggested once it has been used this often.</summary>
    internal const int MinUsesForUnnamedUnit = 3;

    /// <summary>
    /// Autocomplete source. Offers only "real" units: those with a long name (<c>Name</c> differs
    /// from <c>ShortName</c>) or used at least <see cref="MinUsesForUnnamedUnit"/> times across
    /// items and purchase history, so one-off typos stop being suggested back.
    /// </summary>
    [HttpGet("search")]
    public IEnumerable<UnitViewModel> SearchItems([FromQuery] string pattern = "")
    {
        pattern = (pattern ?? "").ToLower();

        var q = from u in this.repo.Units
                where u.Name.ToLower().Contains(pattern) ||
                      u.ShortName.ToLower().Contains(pattern)
                where (u.Name != null && u.Name != "" && u.Name != u.ShortName) ||
                      this.repo.ShoppingListItems.Count(i => i.UnitId == u.Id) +
                      this.repo.ItemHistories.Count(h => h.UnitId == u.Id) >= MinUsesForUnnamedUnit
                orderby u.ShortName
                select u;

        return this.mapper.ProjectTo<UnitViewModel>(q);
    }
}
