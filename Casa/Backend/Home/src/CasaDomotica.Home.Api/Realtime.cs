using System.Collections.Concurrent;
using System.Security.Claims;
using CasaDomotica.Home.Domain.Interfaces;
using Microsoft.AspNetCore.SignalR;

public sealed record HouseSubscription(string ConnectionId, Guid HouseId, UserContext User, long TokenExpiresAt);
public sealed class HouseSubscriptions
{
    private readonly ConcurrentDictionary<string, HouseSubscription> _connections = new();
    public void Add(HouseSubscription value) => _connections[value.ConnectionId] = value;
    public void Remove(string id) => _connections.TryRemove(id, out _);
    public HouseSubscription[] For(Guid houseId) => _connections.Values.Where(x => x.HouseId == houseId).ToArray();
}
public sealed class HouseHub(IUserAuthorizer authorizer, HouseSubscriptions subscriptions) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var request = Context.GetHttpContext()?.Request;
        if (!Guid.TryParse(Context.User?.FindFirstValue("house_id") ?? request?.Query["houseId"].ToString(), out var houseId) || !Guid.TryParse(Context.User?.FindFirstValue("sub"), out var userId) || !Guid.TryParse(Context.User?.FindFirstValue("sid"), out var sessionId) || !long.TryParse(Context.User?.FindFirstValue("access_expires") ?? Context.User?.FindFirstValue("exp"), out var expires)) { Context.Abort(); return; }
        var user = new UserContext(userId, sessionId);
        if (!await authorizer.CheckAsync(user, houseId, "home:read", Guid.NewGuid(), Context.ConnectionAborted)) { Context.Abort(); return; }
        subscriptions.Add(new(Context.ConnectionId, houseId, user, expires));
        await base.OnConnectedAsync();
    }
    public override Task OnDisconnectedAsync(Exception? exception) { subscriptions.Remove(Context.ConnectionId); return base.OnDisconnectedAsync(exception); }
}
public sealed class SignalRStateNotifier(IHubContext<HouseHub> hub, HouseSubscriptions subscriptions, IUserAuthorizer authorizer) : IStateNotifier
{
    public async Task NotifyAsync(Guid houseId, CancellationToken ct)
    {
        foreach (var subscriber in subscriptions.For(houseId))
        {
            if (subscriber.TokenExpiresAt <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() || !await authorizer.CheckAsync(subscriber.User, houseId, "home:read", Guid.NewGuid(), ct))
            {
                subscriptions.Remove(subscriber.ConnectionId);
                await hub.Clients.Client(subscriber.ConnectionId).SendAsync("permissionRevoked", cancellationToken: ct);
                continue;
            }
            // A notification invalidates authorized REST queries; it never carries private device data.
            await hub.Clients.Client(subscriber.ConnectionId).SendAsync("stateChanged", new { houseId }, ct);
        }
    }
}
