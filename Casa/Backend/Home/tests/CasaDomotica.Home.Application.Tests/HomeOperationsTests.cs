using System.Text.Json;
using CasaDomotica.Home.Application.Commands;
using CasaDomotica.Home.Domain.Interfaces;
using Xunit;
namespace CasaDomotica.Home.Application.Tests;
public sealed class HomeOperationsTests
{
    private sealed class Authorizer(bool allowed) : IUserAuthorizer { public Task<bool> CheckAsync(UserContext user, Guid houseId, string action, Guid correlationId, CancellationToken ct) => Task.FromResult(allowed); }
    private sealed class Store : IHomeRuntime
    {
        public int Admissions;
        public Task<StoredStatus?> StatusAsync(Guid houseId, CancellationToken ct) => Task.FromResult<StoredStatus?>(null);
        public Task<StoredCommand?> FindAsync(string commandId, CancellationToken ct) => Task.FromResult<StoredCommand?>(null);
        public Task<StoredCommand?> FindByKeyAsync(Guid houseId, string key, CancellationToken ct) => Task.FromResult<StoredCommand?>(null);
        public Task<StoredCommand> AdmitAsync(UserContext user, Guid houseId, string lightId, string type, string payloadJson, string requestHash, string key, Guid correlationId, DateTimeOffset requestedAt, DateTimeOffset expiresAt, CancellationToken ct)
        {
            Admissions++;
            return Task.FromResult(new StoredCommand("isolated-order", houseId, lightId, "isolated-device", type, payloadJson, requestHash, key, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(30), correlationId, "pending", null));
        }
    }
    [Fact]
    public async Task Unauthorized_users_cannot_create_orders()
    {
        var store = new Store(); var operation = new HomeOperations(store, new Authorizer(false));
        var error = await Assert.ThrowsAsync<HomeException>(() => operation.SendAsync(new(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid(), "light", "SetLight", JsonSerializer.SerializeToElement(new { on = true }), Guid.NewGuid().ToString(), Guid.NewGuid(), CancellationToken.None));
        Assert.Equal("forbidden", error.Code); Assert.Equal(0, store.Admissions);
    }
    [Fact]
    public async Task Numeric_values_do_not_substitute_boolean_actuator_payloads()
    {
        var store = new Store(); var operation = new HomeOperations(store, new Authorizer(true));
        var error = await Assert.ThrowsAsync<HomeException>(() => operation.SendAsync(new(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid(), "light", "SetLight", JsonSerializer.SerializeToElement(new { on = 1 }), Guid.NewGuid().ToString(), Guid.NewGuid(), CancellationToken.None));
        Assert.Equal("invalidPayload", error.Code); Assert.Equal(0, store.Admissions);
    }
    [Fact]
    public async Task An_accepted_order_stays_pending_without_a_device_result()
    {
        var store = new Store(); var operation = new HomeOperations(store, new Authorizer(true));
        var result = await operation.SendAsync(new(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid(), "light", "SetLight", JsonSerializer.SerializeToElement(new { on = true }), Guid.NewGuid().ToString(), Guid.NewGuid(), CancellationToken.None);
        Assert.Equal("pending", result.Status); Assert.Null(result.ResultJson); Assert.Equal(1, store.Admissions);
    }
}
