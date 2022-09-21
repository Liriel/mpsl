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
    private readonly INotificationService notificationService;

    public ItemController(IEntityService<ShoppingListItem> entitySvc, IRepository repo, ILogger<ItemController> logger, IMapper mapper, IUnitService unitService,
        INotificationService notificationService)
        : base(entitySvc, repo, logger, mapper)
    {
        this.notificationService = notificationService;
        this.unitService = unitService;
        this.repo = repo;
        this.logger = logger;
        this.mapper = mapper;
    }

    protected override void PostMapping(ShoppingListItemEditViewModel viewModel, ShoppingListItem model)
    {
        model.Unit = this.unitService.GetOrCreateUnit(viewModel.UnitShortName);
    }

    [HttpPut("{itemId}/toggle")]
    public IActionResult ToggleStatus(int itemId)
    {

        // TODO: check access
        var item = this.repo.Find<ShoppingListItem>(itemId);

        if (item == null)
            return new NotFoundObjectResult("item not found");

        if (item.Status == ItemState.Archived)
            return new BadRequestObjectResult("cant toggle archived items");

        if (item.Status == ItemState.Open)
        {
            item.Status = ItemState.Checked;
            item.CheckDate = DateTime.Now;
        }
        else
        {
            item.Status = ItemState.Open;
            item.CheckDate = null;
        }

        this.repo.SaveChanges();
        this.notificationService.BroadCastItemChanged(item);
        return Ok();
    }
}