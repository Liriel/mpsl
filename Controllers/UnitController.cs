using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using mps.Model;
using mps.Services;
using mps.ViewModels;

namespace mps.Controllers;

[ApiController]
[Route("[controller]")]
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
}