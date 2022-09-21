using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using mps.Model;
using mps.Services;
using mps.ViewModels;

namespace mps.Controllers;

[ApiController]
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

    [HttpGet("search/{pattern?}")]
    public IEnumerable<UnitViewModel> SearchItems(string pattern = "")
    {
        var q = from u in this.repo.Units
                where u.Name.ToLower().Contains(pattern.ToLower()) ||
                      u.ShortName.ToLower().Contains(pattern.ToLower())
                orderby u.ShortName
                select u;

        return this.mapper.ProjectTo<UnitViewModel>(q);
    }
}