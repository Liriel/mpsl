using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using mps.Infrastructure;
using mps.Model;
using mps.Services;
using mps.ViewModels;

namespace mps.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ItemController : EntityController<ShoppingListItem, ShoppingListItemEditViewModel, ShoppingListItemViewModel>
{
    private readonly ILogger<ItemController> logger;
    private readonly IRepository repo;
    private readonly IMapper mapper;
    private readonly IUnitService unitService;

    public ItemController(IEntityService<ShoppingListItem> entitySvc, IRepository repo, ILogger<ItemController> logger, IMapper mapper, IUnitService unitService)
        : base(entitySvc, repo, logger, mapper)
    {
        this.unitService = unitService;
        this.repo = repo;
        this.logger = logger;
        this.mapper = mapper;
    }

    protected override void PostMapping(ShoppingListItemEditViewModel viewModel, ShoppingListItem model){
        model.Unit = this.unitService.GetOrCreateUnit(viewModel.UnitShortName);

    }
}