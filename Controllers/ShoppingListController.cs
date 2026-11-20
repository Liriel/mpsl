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
    private readonly IItemArchiveService archiveService;
    private readonly IItemMatchService matchService;
    private readonly IItemMergeService mergeService;
    private readonly IItemKindClassifier kindClassifier;

    public ShoppingListController(IEntityService<ShoppingList> entitySvc, IRepository repo, ILogger<ShoppingListController> logger, IMapper mapper, IUnitService unitService,
        IEntityService<ShoppingListItem> itemService, IUserIdentityService userIdentityService, IShoppingListAccessService accessService, IDeclutterService declutterService, IItemArchiveService archiveService,
        IItemMatchService matchService, IItemMergeService mergeService, IItemKindClassifier kindClassifier)
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
        this.archiveService = archiveService;
        this.matchService = matchService;
        this.mergeService = mergeService;
        this.kindClassifier = kindClassifier;
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

        DateTime minDate = DateTime.UtcNow.AddHours(-1);

        this.archiveService.ArchiveStale(shoppingListId, minDate);

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

        if (!ModelState.IsValid)
            return new BadRequestObjectResult(
                new WebOperationResult<ShoppingListAddViewModel, ShoppingListItem>(ModelState));

        if (itemViewModel.Id != 0)
            return AddItemFailure("Use item controller to update items", nameof(itemViewModel.Id));

        // "!Tanken" is an explicit task; the prefix is not part of the name
        var typedName = itemViewModel.Name;
        var forcedTask = ItemKindClassifier.StripTaskPrefix(ref typedName);
        if (forcedTask)
        {
            if (string.IsNullOrWhiteSpace(typedName))
                return AddItemFailure("The name must not be empty", nameof(itemViewModel.Name));

            itemViewModel.Name = typedName;
        }

        // NormalizedName is not unique - only Name+ShoppingListId is - so several rows can
        // collapse onto the same normalized value ("Öl" and "Oel" both become OEL). Prefer the
        // exact name match and otherwise take the oldest row, rather than throwing a 500.
        var normalizedName = NameHelper.GetNormalizedName(itemViewModel.Name);
        var candidates = this.repo.ShoppingListItems
            .Where(i => i.ShoppingListId == shoppingListId &&
                   i.NormalizedName == normalizedName)
            .OrderBy(i => i.Id)
            .ToList();

        var item = candidates.FirstOrDefault(i => i.Name == itemViewModel.Name)
            ?? candidates.FirstOrDefault();

        // explicit Kind > "!" prefix > (new items only) the classifier. A re-added item keeps the
        // kind it has - that is how a user correction sticks.
        ItemKind? explicitKind = itemViewModel.Kind ?? (forcedTask ? ItemKind.Task : null);
        var autoClassified = false;
        string kindReason = null;

        if (item == null)
        {
            item = this.mapper.Map<ShoppingListItem>(itemViewModel);
            item.ShoppingListId = shoppingListId;

            if (explicitKind.HasValue)
            {
                item.Kind = explicitKind.Value;
            }
            else
            {
                var otherListNames = this.accessService.FilterAccessible(this.repo.GetEntities<ShoppingList>(), this.User)
                    .Where(l => l.Id != shoppingListId)
                    .Select(l => l.Name)
                    .ToList();
                (item.Kind, kindReason) = this.kindClassifier.Classify(item.Name, otherListNames);
                autoClassified = true;
            }
        }
        else
        {
            item.Amount = itemViewModel.Amount;
            item.Status = ItemState.Open;
            if (explicitKind.HasValue)
                item.Kind = explicitKind.Value;
        }

        // Same rule as ItemController.PostMapping: a null short name leaves whatever unit the
        // item already carries in place, an empty one clears it.
        if (itemViewModel.UnitShortName != null)
        {
            // "500g" typed into the unit field with no amount becomes amount 500 + unit g
            var (amount, unitText) = UnitParser.Split(itemViewModel.Amount, itemViewModel.UnitShortName);
            item.Amount = amount;

            // local, not item.Unit - see ItemController.PostMapping
            var unitResult = this.unitService.GetOrCreateUnit(unitText);
            if (!unitResult.Success)
                return AddItemFailure(unitResult.ValidationErrors[0].Message, nameof(itemViewModel.UnitShortName));

            item.Unit = unitResult.Entity;
            item.UnitId = unitResult.Entity?.Id;
        }

        item.AddDate = DateTime.UtcNow;
        item.AddedByUserId = this.userIdentityService.CurrentUserId;
        var saveResult = this.itemService.AddOrUpdate(item);

        var result = new WebOperationResult<ShoppingListAddViewModel, ShoppingListItem>(this.mapper, saveResult);
        if (result.Entity != null)
        {
            result.Entity.KindAuto = autoClassified;
            result.Entity.KindReason = kindReason;
        }

        return result.Success
            ? new OkObjectResult(result)
            : new BadRequestObjectResult(result);
    }

    /// <summary>
    /// Rejects an add with the same <see cref="WebOperationResult{TEditViewModel, TEntity}"/> shape
    /// the rest of the write endpoints use, rather than a bare string.
    /// </summary>
    private BadRequestObjectResult AddItemFailure(string message, params string[] fields)
    {
        var result = new WebOperationResult<ShoppingListAddViewModel, ShoppingListItem> { Success = false };
        result.ValidationErrors.Add(new ValidationError(message, fields));
        return new BadRequestObjectResult(result);
    }

    /// <summary>An item without history that is removed within this many minutes is deleted, not archived.</summary>
    private const int FreshItemMinutes = 10;

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

        // A typo removed right after it was added (no purchase ever recorded) leaves nothing behind:
        // archiving it would only add clutter for the declutter tool to find later. Going through
        // the entity service keeps the SignalR removal broadcast.
        if (item.AddDate > DateTime.UtcNow.AddMinutes(-FreshItemMinutes)
            && !this.repo.ItemHistories.Any(h => h.ShoppingListItemId == itemId))
        {
            this.itemService.Delete(item);
            return Ok();
        }

        item.Status = ItemState.Archived;
        var saveResult = this.itemService.AddOrUpdate(item);

        return Ok();
    }

    [HttpGet("{shoppingListId}/search")]
    public IActionResult SearchItems(int shoppingListId, [FromQuery] string pattern = "")
    {
        if (this.repo.Find<ShoppingList>(shoppingListId) == null)
            return new NotFoundObjectResult("Invalid shopping list");

        if (!this.accessService.CanAccessList(shoppingListId, this.User))
            return Forbid();

        // replace umlauts
        pattern = NameHelper.GetNormalizedName(pattern);

        // also match on the fuzzy key, so "passtomaten" finds "Pass Tomaten"
        var keyMatches = this.matchService.FindByMatchKey(shoppingListId, pattern);

        var q = from i in this.repo.ShoppingListItems
                where i.ShoppingListId == shoppingListId
                where i.NormalizedName.Contains(pattern) || keyMatches.Contains(i.Id)
                orderby i.Name
                select i;

        return Ok(this.mapper.ProjectTo<ShoppingListItemViewModel>(q));
    }

    /// <summary>
    /// Existing items close to <paramref name="name"/> ("Did you mean ...?"), best-established first.
    /// Empty when the name already matches an item exactly or nothing is near.
    /// </summary>
    [HttpGet("{shoppingListId}/suggest")]
    public IActionResult SuggestItems(int shoppingListId, [FromQuery] string name = "")
    {
        if (this.repo.Find<ShoppingList>(shoppingListId) == null)
            return new NotFoundObjectResult("Invalid shopping list");

        if (!this.accessService.CanAccessList(shoppingListId, this.User))
            return Forbid();

        var matches = this.matchService.Suggest(shoppingListId, name ?? string.Empty);
        return Ok(matches.Select(ToViewModel));
    }

    // not AutoMapper: AutoMapper 11 cannot scan a positional record on .NET 10 (it throws while
    // probing LINQ extension methods) and takes every other mapping in the app down with it
    private static ItemMatchViewModel ToViewModel(ItemMatch m) => new ItemMatchViewModel
    {
        Id = m.Id,
        Name = m.Name,
        ShortName = m.ShortName,
        Status = m.Status.ToString(),
        PurchaseCount = m.PurchaseCount,
        LastPurchaseDate = m.LastPurchaseDate
    };

    /// <summary>Suggested groups of probable duplicate items, input for the merge tool.</summary>
    [HttpGet("{shoppingListId}/duplicates")]
    public IActionResult GetDuplicateGroups(int shoppingListId)
    {
        if (this.repo.Find<ShoppingList>(shoppingListId) == null)
            return new NotFoundObjectResult("Invalid shopping list");

        if (!this.accessService.CanAccessList(shoppingListId, this.User))
            return Forbid();

        var groups = this.matchService.FindDuplicateGroups(shoppingListId)
            .Select(g => g.Items.Select(ToViewModel).ToList())
            .ToList();
        return Ok(groups);
    }

    /// <summary>Folds duplicate items into a surviving one, keeping their purchase history.</summary>
    [HttpPost("{shoppingListId}/merge")]
    public IActionResult MergeItems(int shoppingListId, [FromBody] ItemMergeViewModel model)
    {
        if (this.repo.Find<ShoppingList>(shoppingListId) == null)
            return new NotFoundObjectResult("Invalid shopping list");

        if (!this.accessService.CanAccessList(shoppingListId, this.User))
            return Forbid();

        var result = this.mergeService.Merge(shoppingListId, model.TargetId, model.SourceIds);
        if (!result.Success)
            return new BadRequestObjectResult(new ValidationFailureResult { ValidationErrors = result.ValidationErrors.ToList() });

        return Ok(this.mapper.Map<ShoppingListItemViewModel>(result.Entity));
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
