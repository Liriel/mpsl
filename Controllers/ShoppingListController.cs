using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using mps.Infrastructure;
using mps.Model;
using mps.Services;
using mps.ViewModels;

namespace mps.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ShoppingListController : EntityController<ShoppingList, ShoppingListEditViewModel, ShoppingListViewModel>
{
    private readonly ILogger<ShoppingListController> logger;
    private readonly IRepository repo;
    private readonly IMapper mapper;

    public ShoppingListController(IEntityService<ShoppingList> entitySvc, IRepository repo, ILogger<ShoppingListController> logger, IMapper mapper)
        : base(entitySvc, repo, logger, mapper)
    {
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
    public IEnumerable<ShoppingListItemViewModel> GetItems(int shoppingListId){
        var q = this.repo.ShoppingListItems.Where(i => i.ShoppingListId == shoppingListId);

        return this.mapper.ProjectTo<ShoppingListItemViewModel>(q);
    }

    [HttpPost("{shoppingListId}/item")]
    public IActionResult AddItem(int shoppingListId, ShoppingListItemEditViewModel itemViewModel){
        var shoppingList = this.repo.Find<ShoppingList>(shoppingListId);
        if(shoppingList == null)
            return new NotFoundObjectResult("shopping list not found");

        var item = this.mapper.Map<ShoppingListItem>(itemViewModel);
        item.Unit = GetOrCreateUnit(itemViewModel.UnitShortName);
        shoppingList.Items.Add(item);
        this.repo.SaveChanges();

        return Ok();
    }

    [HttpPut("{shoppingListId}/item/{itemId}")]
    public IActionResult UpdateItem(int shoppingListId, int itemId, ShoppingListItemEditViewModel itemViewModel)
    {
        var item = this.repo.ShoppingListItems.SingleOrDefault(i => i.ShoppingListId == shoppingListId && i.Id == itemId);
        if (item == null)
            return new NotFoundObjectResult("shopping list or item not found");

        this.mapper.Map(itemViewModel, item);
        item.Unit = GetOrCreateUnit(itemViewModel.UnitShortName);

        this.repo.SaveChanges();
        return Ok();
    }

    private Unit GetOrCreateUnit(string unitShortName)
    {
        // check if the unit exists or should be created
        if (string.IsNullOrWhiteSpace(unitShortName))
            return null;

        var unit = this.repo.Units.SingleOrDefault(u => u.ShortName.ToLower() == unitShortName.ToLower());
        if (unit == null)
        {
            unit = new Unit { ShortName = unitShortName };
        }
        return unit;
    }
}
