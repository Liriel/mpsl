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
public class ShoppingListController : EntityController<ShoppingList, ShoppingListEditViewModel, ShoppingListViewModel>
{
    private readonly ILogger<ShoppingListController> logger;
    private readonly IRepository repo;
    private readonly IMapper mapper;
    private readonly IUnitService unitService;
    private readonly IEntityService<ShoppingListItem> itemService;
    private readonly IUserIdentityService userIdentityService;
    private readonly IShoppingListAccessService accessService;
    private readonly IDeclutterService declutterService;

    public ShoppingListController(IEntityService<ShoppingList> entitySvc, IRepository repo, ILogger<ShoppingListController> logger, IMapper mapper, IUnitService unitService,
        IEntityService<ShoppingListItem> itemService, IUserIdentityService userIdentityService, IShoppingListAccessService accessService, IDeclutterService declutterService)
        : base(entitySvc, repo, logger, mapper)
    {
        this.userIdentityService = userIdentityService;
        this.itemService = itemService;
        this.unitService = unitService;
        this.repo = repo;
        this.logger = logger;
        this.mapper = mapper;
        this.accessService = accessService;
        this.declutterService = declutterService;
    }

    protected override IQueryable<ShoppingList> ApplyAccessFilter(IQueryable<ShoppingList> query) =>
        this.accessService.FilterAccessible(query, this.User);

    protected override bool CanAccess(ShoppingList entity) =>
        this.accessService.CanAccessList(entity.Id, this.User);


    [HttpGet] // Explicitly adding the attribute here "overrides" the EntityControllers index method
    public IActionResult FilteredIndex(string name, int skip = 0, int take = 100, string sort = null, SortDirection sortDirection = SortDirection.asc)
    {
        var q = this.accessService.FilterAccessible(this.repo.GetEntities<ShoppingList>(), this.User);

        // HACK: it looks like string.Contains() is correctly translated here but sqlite
        // is case sensitive?
        if (!string.IsNullOrWhiteSpace(name))
            q = q.Where(l => l.Name.ToLower().Contains(name.ToLower()));

        q = this.ApplySort(q, sort, sortDirection);
        return PageAndProjectResult(q, skip, take);
    }

    [HttpGet("{shoppingListId}/recommendations")]
    public IActionResult GetRecommendations(int shoppingListId)
    {
        if (this.repo.Find<ShoppingList>(shoppingListId) == null)
            return new NotFoundObjectResult("Invalid shopping list");

        if (!this.accessService.CanAccessList(shoppingListId, this.User))
            return Forbid();

        var q = from r in this.repo.Recommendations
            where r.ShoppingListId == shoppingListId
            orderby r.Rank
            select r;

        return Ok(this.mapper.ProjectTo<RecommendationViewModel>(q.Take(100)));
    }

    [HttpGet("{shoppingListId}/item")]
    public IActionResult GetItems(int shoppingListId)
    {
        if (this.repo.Find<ShoppingList>(shoppingListId) == null)
            return new NotFoundObjectResult("Invalid shopping list");

        if (!this.accessService.CanAccessList(shoppingListId, this.User))
            return Forbid();

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

        return Ok(this.mapper.ProjectTo<ShoppingListItemViewModel>(q));
    }

    [HttpPost("{shoppingListId}/add")]
    public IActionResult AddItem(int shoppingListId, ShoppingListAddViewModel itemViewModel)
    {
        if (this.repo.Find<ShoppingList>(shoppingListId) == null)
            return new NotFoundObjectResult("Invalid shopping list");

        if (!this.accessService.CanAccessList(shoppingListId, this.User))
            return Forbid();

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
            return new NotFoundObjectResult("Invalid shopping list");

        if (!this.accessService.CanAccessList(shoppingListId, this.User))
            return Forbid();

        var item = shoppingList.Items.SingleOrDefault(i => i.Id == itemId);
        if (item == null)
            return new BadRequestObjectResult("Item not found in shopping list");

        item.Status = ItemState.Archived;
        var saveResult = this.itemService.AddOrUpdate(item);

        return Ok();
    }

    [HttpGet("{shoppingListId}/search/{pattern?}")]
    public IActionResult SearchItems(int shoppingListId, string pattern = "")
    {
        if (this.repo.Find<ShoppingList>(shoppingListId) == null)
            return new NotFoundObjectResult("Invalid shopping list");

        if (!this.accessService.CanAccessList(shoppingListId, this.User))
            return Forbid();

        // replace umlauts
        pattern = NameHelper.GetNormalizedName(pattern);

        var q = from i in this.repo.ShoppingListItems
                where i.ShoppingListId == shoppingListId
                where i.NormalizedName.Contains(pattern)
                orderby i.Name
                select i;

        return Ok(this.mapper.ProjectTo<ShoppingListItemViewModel>(q));
    }

    /// <summary>
    /// Get de-clutter candidates for a shopping list.
    /// Returns items that have been purchased rarely and/or haven't been purchased recently.
    /// </summary>
    /// <param name="shoppingListId">The shopping list to analyze.</param>
    /// <param name="minMonthsSinceLastPurchase">Minimum months since last purchase to be considered stale (default: 6).</param>
    /// <param name="maxPurchaseCount">Maximum purchase count to be considered rarely purchased (default: 2).</param>
    /// <returns>List of de-clutter candidate items with purchase statistics.</returns>
    [HttpGet("{shoppingListId}/declutter")]
    public IActionResult GetDeclutterCandidates(
        int shoppingListId,
        [FromQuery] int minMonthsSinceLastPurchase = 6,
        [FromQuery] int maxPurchaseCount = 2)
    {
        if (this.repo.Find<ShoppingList>(shoppingListId) == null)
            return new NotFoundObjectResult("Invalid shopping list");

        if (!this.accessService.CanAccessList(shoppingListId, this.User))
            return Forbid();

        var candidates = this.declutterService.GetDeclutterCandidates(
            shoppingListId,
            minMonthsSinceLastPurchase,
            maxPurchaseCount);

        var viewModels = this.mapper.Map<IEnumerable<DeclutterCandidateViewModel>>(candidates);
        return Ok(viewModels);
    }

    /// <summary>
    /// Bulk delete shopping list items (typically used for de-cluttering).
    /// </summary>
    /// <param name="shoppingListId">The shopping list that owns the items.</param>
    /// <param name="itemIds">Array of item IDs to delete.</param>
    /// <returns>Number of items successfully deleted.</returns>
    [HttpPost("{shoppingListId}/declutter")]
    public IActionResult BulkDeleteItems(int shoppingListId, [FromBody] int[] itemIds)
    {
        if (this.repo.Find<ShoppingList>(shoppingListId) == null)
            return new NotFoundObjectResult("Invalid shopping list");

        if (!this.accessService.CanAccessList(shoppingListId, this.User))
            return Forbid();

        if (itemIds == null || itemIds.Length == 0)
            return new BadRequestObjectResult("No items specified");

        int deletedCount = this.declutterService.BulkDeleteItems(shoppingListId, itemIds);

        return Ok(new { DeletedCount = deletedCount });
    }
}
