using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using mps.Services;

namespace mps.Hubs
{
    [Authorize(Policy = "ApprovedUser")]
    public class LiveHub : Hub
    {
        private readonly ILogger<LiveHub> logger;
        private readonly IShoppingListAccessService accessService;

        public LiveHub(ILogger<LiveHub> logger, IShoppingListAccessService accessService)
        {
            this.logger = logger;
            this.accessService = accessService;
        }

        public override async Task OnConnectedAsync()
        {
            this.logger.LogInformation($"added connection {this.Context.ConnectionId}");
            await base.OnConnectedAsync();
        }

        public async Task JoinList(int shoppingListId)
        {
            if (!this.accessService.CanAccessList(shoppingListId, this.Context.User))
                throw new HubException("Access denied");

            await this.Groups.AddToGroupAsync(this.Context.ConnectionId, $"sl{shoppingListId}");
        }

        public async Task LeaveList(int shoppingListId)
        {
            if (!this.accessService.CanAccessList(shoppingListId, this.Context.User))
                throw new HubException("Access denied");

            await this.Groups.RemoveFromGroupAsync(this.Context.ConnectionId, $"sl{shoppingListId}");
        }
    }
}
