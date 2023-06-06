using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using mps.Infrastructure;
using mps.Model;
using mps.Services;
using mps.ViewModels;

namespace mps.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ShoppingListController : EntityController<ShoppingList, ShoppingListEditViewModel, ShoppingListViewModel>
{
    private readonly ILogger<ShoppingListController> logger;
    private readonly IRepository repo;
    private readonly IMapper mapper;
    private readonly IUnitService unitService;
    private readonly IEntityService<ShoppingListItem> itemService;
    private readonly IUserIdentityService userIdentityService;

    public ShoppingListController(IEntityService<ShoppingList> entitySvc, IRepository repo, ILogger<ShoppingListController> logger, IMapper mapper, IUnitService unitService,
        IEntityService<ShoppingListItem> itemService, IUserIdentityService userIdentityService)
        : base(entitySvc, repo, logger, mapper)
    {
        this.userIdentityService = userIdentityService;
        this.itemService = itemService;
        this.unitService = unitService;
        this.repo = repo;
        this.logger = logger;
        this.mapper = mapper;
    }


    [HttpGet] // Explicitly adding the attribute here "overrides" the EntityControllers index method
    public IActionResult FilteredIndex(string name, int skip = 0, int take = 100, string sort = null, SortDirection sortDirection = SortDirection.asc)
    {
        var q = this.repo.GetEntities<ShoppingList>();

        // TODO: filter for owned lists
        // HACK: it looks like string.Contains() is correctly translated here but sqlite
        // is case sensitive?
        if (!string.IsNullOrWhiteSpace(name))
            q = q.Where(l => l.Name.ToLower().Contains(name.ToLower()));

        q = this.ApplySort(q, sort, sortDirection);
        return PageAndProjectResult(q, skip, take);
    }

    [HttpGet("{shoppingListId}/item")]
    public IEnumerable<ShoppingListItemViewModel> GetItems(int shoppingListId)
    {
        DateTime minDate = DateTime.Now.AddHours(-1);

        // find old items
        var o = from i in this.repo.ShoppingListItems
                where i.ShoppingListId == shoppingListId
                where i.Status == ItemState.Checked && i.CheckDate < minDate
                select i;

        if (o.Any())
        {
            foreach (var item in o)
            {
                item.History.Add(new ItemHistory
                {
                    Amount = item.Amount,
                    UnitId = item.UnitId,
                    CheckDate = item.CheckDate.Value
                });
                item.Status = ItemState.Archived;
            }

            this.repo.SaveChanges();
        }

        var q = from i in this.repo.ShoppingListItems
                where i.ShoppingListId == shoppingListId
                where i.Status == ItemState.Open || (i.Status == ItemState.Checked && i.CheckDate >= minDate)
                orderby i.AddDate
                select i;

        return this.mapper.ProjectTo<ShoppingListItemViewModel>(q);
    }

    [HttpPost("{shoppingListId}/add")]
    public IActionResult AddItem(int shoppingListId, ShoppingListAddViewModel itemViewModel)
    {
        WebOperationResult<ShoppingListAddViewModel, ShoppingListItem> result;
        if (ModelState.IsValid)
        {
            if (itemViewModel.Id != 0)
                return new BadRequestObjectResult("Use item controller to update items");

            var normalizedName = NameHelper.GetNormalizedName(itemViewModel.Name);
            var item = this.repo.ShoppingListItems
                .Where(i => i.ShoppingListId == shoppingListId &&
                       i.NormalizedName == normalizedName)
                .SingleOrDefault();

            if (item == null)
            {
                item = this.mapper.Map<ShoppingListItem>(itemViewModel);
                item.ShoppingListId = shoppingListId;
            }
            else
            {
                item.Amount = itemViewModel.Amount;
                item.Status = ItemState.Open;
            }

            // this ensures that the unit property was loaded by the lazy laoding proxy
            if (item.Unit != null || !string.IsNullOrEmpty(itemViewModel.UnitShortName))
            {
                item.Unit = this.unitService.GetOrCreateUnit(itemViewModel.UnitShortName);
                item.UnitId = item.Unit?.Id;
            }

            item.AddDate = DateTime.Now;
            item.AddedByUserId = this.userIdentityService.CurrentUserId;
            var saveResult = this.itemService.AddOrUpdate(item);

            result = new WebOperationResult<ShoppingListAddViewModel, ShoppingListItem>(mapper, saveResult);
        }
        else
        {
            result = new WebOperationResult<ShoppingListAddViewModel, ShoppingListItem>(ModelState);
        }

        return new OkObjectResult(result);
    }

    [HttpDelete("{shoppingListId}/remove/{itemId}")]
    public IActionResult RemoveItem(int shoppingListId, int itemId)
    {
        var shoppingList = this.repo.Find<ShoppingList>(shoppingListId);

        if (shoppingList == null)
            return new BadRequestObjectResult("Invalid shopping list");

        var item = shoppingList.Items.SingleOrDefault(i => i.Id == itemId);
        if (item == null)
            return new BadRequestObjectResult("Item not found in shopping list");

        item.Status = ItemState.Archived;
        var saveResult = this.itemService.AddOrUpdate(item);

        return Ok();
    }

    [HttpGet("{shoppingListId}/search/{pattern?}")]
    public IEnumerable<ShoppingListItemViewModel> SearchItems(int shoppingListId, string pattern = "")
    {
        // replace umlauts
        pattern = NameHelper.GetNormalizedName(pattern);

        var q = from i in this.repo.ShoppingListItems
                where i.ShoppingListId == shoppingListId
                where i.NormalizedName.Contains(pattern)
                orderby i.Name
                select i;

        return this.mapper.ProjectTo<ShoppingListItemViewModel>(q);
    }
}
