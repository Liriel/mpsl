using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using mps.Model;
using Xunit;

namespace Tests;

/// <summary>
/// F3: /live must reject anonymous and non-approved connections, and clients must only receive
/// events for the shopping lists they explicitly joined.
/// </summary>
public class LiveHubTests : IAsyncLifetime
{
    private readonly TestAppFactory factory = new TestAppFactory();
    private string approvedUserId = null!;
    private int list1Id;
    private int list2Id;

    public async Task InitializeAsync()
    {
        using var scope = this.factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = "hub-user@test.local", Email = "hub-user@test.local" };
        var result = await userManager.CreateAsync(user);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));
        this.approvedUserId = user.Id;

        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var list1 = new ShoppingList { Name = "Hub List 1" };
        var list2 = new ShoppingList { Name = "Hub List 2" };
        db.ShoppingLists.AddRange(list1, list2);
        await db.SaveChangesAsync();
        this.list1Id = list1.Id;
        this.list2Id = list2.Id;
    }

    public Task DisposeAsync()
    {
        this.factory.Dispose();
        return Task.CompletedTask;
    }

    private HubConnection CreateConnection(string? userId = null, string? roles = null)
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(this.factory.Server.BaseAddress, "live"), options =>
            {
                options.HttpMessageHandlerFactory = _ => this.factory.Server.CreateHandler();
                // TestServer has no real socket to upgrade - long polling works over plain HTTP.
                options.Transports = HttpTransportType.LongPolling;
                if (userId != null)
                    options.Headers.Add(TestAuthHandler.UserHeader, userId);
                if (roles != null)
                    options.Headers.Add(TestAuthHandler.RolesHeader, roles);
            })
            .Build();
    }

    [Fact]
    public async Task AnonymousConnection_IsRejected()
    {
        await using var connection = this.CreateConnection();
        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
    }

    [Fact]
    public async Task NoRoleUser_IsRejected()
    {
        await using var connection = this.CreateConnection(this.approvedUserId);
        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
    }

    [Fact]
    public async Task JoinList_OnNonexistentList_ThrowsHubException()
    {
        await using var connection = this.CreateConnection(this.approvedUserId, "user");
        await connection.StartAsync();

        await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("JoinList", int.MaxValue));
    }

    [Fact]
    public async Task GroupIsolation_OnlyMembersOfTheJoinedListReceiveTheEvent()
    {
        await using var conn1 = this.CreateConnection(this.approvedUserId, "user");
        await using var conn2 = this.CreateConnection(this.approvedUserId, "user");

        var list1ReceivedCount = 0;
        var list2ReceivedCount = 0;
        var list1Received = new TaskCompletionSource<bool>();

        conn1.On<object>("OnItemChanged", _ =>
        {
            Interlocked.Increment(ref list1ReceivedCount);
            list1Received.TrySetResult(true);
        });
        conn2.On<object>("OnItemChanged", _ => Interlocked.Increment(ref list2ReceivedCount));

        await conn1.StartAsync();
        await conn2.StartAsync();

        await conn1.InvokeAsync("JoinList", this.list1Id);
        await conn2.InvokeAsync("JoinList", this.list2Id);

        var httpClient = this.factory.CreateClient();
        httpClient.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, this.approvedUserId);
        httpClient.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "user");

        var addResponse = await httpClient.PostAsJsonAsync(
            $"api/ShoppingList/{this.list1Id}/add",
            new { Name = $"Bread {Guid.NewGuid():N}" });
        var addBody = await addResponse.Content.ReadAsStringAsync();
        Assert.True(addResponse.IsSuccessStatusCode, $"got {addResponse.StatusCode}: {addBody}");

        var completed = await Task.WhenAny(list1Received.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        Assert.True(completed == list1Received.Task, "connection joined to list 1 should have received OnItemChanged");

        // give list 2's connection a moment to (not) receive anything before asserting isolation
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        Assert.Equal(1, list1ReceivedCount);
        Assert.Equal(0, list2ReceivedCount);
    }
}
