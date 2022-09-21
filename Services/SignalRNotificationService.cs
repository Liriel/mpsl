using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using mps.Hubs;
using mps.Model;
using mps.ViewModels;

namespace mps.Services;

public class SignalRNotificationService : INotificationService
{
    private readonly ILogger<SignalRNotificationService> logger;
    private readonly IHubContext<LiveHub> hubContext;
    private readonly IMapper mapper;

    public SignalRNotificationService(ILogger<SignalRNotificationService> logger, IHubContext<LiveHub> hubContext, IMapper mapper)
    {
        this.mapper = mapper;
        this.hubContext = hubContext;
        this.logger = logger;
    }

    public async Task BroadCastItemChanged(ShoppingListItem item)
    {
        string groupName = $"sl{item.ShoppingListId}";
        var itemViewModel = this.mapper.Map<ShoppingListItemViewModel>(item);
        // await this.hubContext.Clients.Group(groupName).SendAsync("OnItemChanged", itemViewModel);
        await this.hubContext.Clients.All.SendAsync("OnItemChanged", itemViewModel);
    }

    public async Task BroadCastItemRemoved(ShoppingListItem item)
    {
        string groupName = $"sl{item.ShoppingListId}";
        // await this.hubContext.Clients.Group(groupName).SendAsync("OnItemChanged", new { item.Id, item.ShoppingListId });
        await this.hubContext.Clients.All.SendAsync("OnItemRemoved", new { item.Id, item.ShoppingListId });
    }
}