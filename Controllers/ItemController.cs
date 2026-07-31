using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using mps.Infrastructure;
using mps.Model;
using mps.Services;
using mps.ViewModels;

namespace mps.Controllers;

[ApiController]
[Authorize(Policy = "ApprovedUser")]
[Route("api/[controller]")]
public class ItemController : EntityController<ShoppingListItem, ShoppingListItemEditViewModel, ShoppingListItemViewModel>
{
    private readonly ILogger<ItemController> logger;
    private readonly IRepository repo;
    private readonly IMapper mapper;
    private readonly IUnitService unitService;
    private readonly INotificationService notificationService;
    private readonly IUserIdentityService userIdentityService;
    private readonly IShoppingListAccessService accessService;

    public ItemController(IEntityService<ShoppingListItem> entitySvc, IRepository repo, ILogger<ItemController> logger, IMapper mapper, IUnitService unitService,
        INotificationService notificationService, IUserIdentityService userIdentityService, IShoppingListAccessService accessService)
        : base(entitySvc, repo, logger, mapper)
    {
        this.userIdentityService = userIdentityService;
        this.notificationService = notificationService;
        this.unitService = unitService;
        this.repo = repo;
        this.logger = logger;
        this.mapper = mapper;
        this.accessService = accessService;
    }

    protected override void PostMapping(ShoppingListItemEditViewModel viewModel, ShoppingListItem model)
    {
        // A null UnitShortName means the caller did not supply one and the existing unit must be
        // left alone; an explicitly empty string clears it. Overwriting unconditionally silently
        // wiped the unit on every edit that omitted the field.
        if (viewModel.UnitShortName == null)
            return;

        // Keep the unit in a local: reading model.Unit back would go through the lazy loading
        // proxy and re-fetch the unit we just cleared, so UnitId would never become null.
        var unit = this.unitService.GetOrCreateUnit(viewModel.UnitShortName);
        model.Unit = unit;
        model.UnitId = unit?.Id;
    }

    protected override bool CanAccess(ShoppingListItem entity) =>
        this.accessService.CanAccessItem(entity.Id, this.User);

    [HttpPut("{itemId}/toggle")]
    public IActionResult ToggleStatus(int itemId)
    {
        var item = this.repo.Find<ShoppingListItem>(itemId);

        if (item == null)
            return new NotFoundObjectResult("item not found");

        if (!this.accessService.CanAccessItem(itemId, this.User))
            return Forbid();

        if (item.Status == ItemState.Archived)
            return new BadRequestObjectResult("cant toggle archived items");

        if (item.Status == ItemState.Open)
        {
            item.Status = ItemState.Checked;
            item.CheckDate = DateTime.Now;
            item.CheckedByUserId = this.userIdentityService.CurrentUserId;
        }
        else
        {
            item.Status = ItemState.Open;
            item.CheckDate = null;
            item.CheckedByUserId = null;
        }

        this.repo.SaveChanges();
        this.notificationService.BroadCastItemChanged(item);
        return Ok();
    }
}