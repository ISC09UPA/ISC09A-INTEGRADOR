using System.Text.Json;
using CasaDomotica.Home.Application.Commands;
using CasaDomotica.Home.Domain.Interfaces;
using Xunit;
namespace CasaDomotica.Home.Application.Tests;
public sealed class CapabilityOperationsTests
{
    private sealed class Clock : TimeProvider { public DateTimeOffset Now = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Authorizer(bool allowed, Action? onCheck = null) : IUserAuthorizer
    {
        public string? ActionChecked;
        public Task<bool> CheckAsync(UserContext user, Guid houseId, string action, Guid correlationId, CancellationToken ct) { ActionChecked = action; onCheck?.Invoke(); return Task.FromResult(allowed); }
    }
    private sealed class Store(Clock? clock = null) : ICapabilityRuntime
    {
        public int Admissions; public string? Payload; public string? Hash; public DateTimeOffset Requested; public DateTimeOffset Expires;
        public Task<StoredCommand> AdmitCapabilityAsync(UserContext user, Guid houseId, string resourceId, string type, string normalizedPayload, string requestHash, string key, Guid correlationId, DateTimeOffset requestedAt, DateTimeOffset expiresAt, CancellationToken ct)
        {
            if (clock is not null && clock.GetUtcNow() >= expiresAt) throw new HomeException("requestExpired", "Expired inside admission transaction.");
            Admissions++; Payload = normalizedPayload; Hash = requestHash; Requested = requestedAt; Expires = expiresAt;
            return Task.FromResult(new StoredCommand(Guid.NewGuid().ToString(), houseId, resourceId, "device", type, normalizedPayload, requestHash, key, 1, requestedAt, expiresAt, correlationId, "pending", null));
        }
    }
    private static readonly UserContext User = new(Guid.NewGuid(), Guid.NewGuid());
    [Theory]
    [InlineData("OpenDoor", "home:control:door")][InlineData("CaptureImage", "home:control:capture")]
    public async Task ServerOnlyCommandsRequireTheirSpecificPermission(string type, string expected)
    {
        var clock = new Clock(); var store = new Store(); var auth = new Authorizer(true);
        var result = await new CapabilityOperations(store, auth, clock).SendAsync(User, Guid.NewGuid(), "resource", type, JsonSerializer.SerializeToElement(new { }), Guid.NewGuid().ToString(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddSeconds(30), default);
        Assert.Equal(expected, auth.ActionChecked); Assert.Equal("pending", result.Status); Assert.Equal("{}", store.Payload);
        Assert.Equal(TimeSpan.FromSeconds(type == "OpenDoor" ? 10 : 30), store.Expires - store.Requested);
    }
    [Fact]
    public async Task ClientCannotChooseDoorIdOrPulseDuration()
    {
        var store = new Store(); var operation = new CapabilityOperations(store, new Authorizer(true), new Clock());
        var payload = JsonSerializer.SerializeToElement(new { doorId = "untrusted", durationMs = 3000 });
        var error = await Assert.ThrowsAsync<HomeException>(() => operation.SendAsync(User, Guid.NewGuid(), "door", "OpenDoor", payload, Guid.NewGuid().ToString(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddSeconds(30), default));
        Assert.Equal("invalidPayload", error.Code); Assert.Equal(0, store.Admissions);
    }
    [Fact]
    public async Task RevokedPermissionOrDelayedAuthorizationNeverAdmitsPhysicalAction()
    {
        var clock = new Clock(); var store = new Store(clock); var house = Guid.NewGuid(); var key = Guid.NewGuid().ToString();
        var operation = new CapabilityOperations(store, new Authorizer(false), clock);
        Assert.Equal("forbidden", (await Assert.ThrowsAsync<HomeException>(() => operation.SendAsync(User, house, "door", "OpenDoor", JsonSerializer.SerializeToElement(new { }), key, Guid.NewGuid(), DateTimeOffset.UtcNow.AddSeconds(30), default))).Code);
        operation = new CapabilityOperations(store, new Authorizer(true, () => clock.Now = clock.Now.AddSeconds(11)), clock);
        Assert.Equal("requestExpired", (await Assert.ThrowsAsync<HomeException>(() => operation.SendAsync(User, house, "door", "OpenDoor", JsonSerializer.SerializeToElement(new { }), key, Guid.NewGuid(), DateTimeOffset.UtcNow.AddSeconds(30), default))).Code);
        Assert.Equal(0, store.Admissions);
    }
    [Fact]
    public async Task SecurityModeAndIdempotencyHashAreCanonical()
    {
        var store = new Store(); var auth = new Authorizer(true); var operation = new CapabilityOperations(store, auth, new Clock());
        var house = Guid.NewGuid(); var key = Guid.NewGuid().ToString(); var correlation = Guid.NewGuid();
        await operation.SendAsync(User, house, "security", "SetSecurityMode", JsonSerializer.SerializeToElement(new { mode = "armed" }), key, correlation, DateTimeOffset.UtcNow.AddSeconds(30), default);
        var firstHash = store.Hash;
        await operation.SendAsync(User, house, "security", "SetSecurityMode", JsonSerializer.SerializeToElement(new { mode = "armed" }), key, correlation, DateTimeOffset.UtcNow.AddSeconds(30), default);
        Assert.Equal(firstHash, store.Hash); Assert.Equal("home:control:security", auth.ActionChecked);
        await operation.SendAsync(User, house, "security", "SetSecurityMode", JsonSerializer.SerializeToElement(new { mode = "disarmed" }), key, correlation, DateTimeOffset.UtcNow.AddSeconds(30), default);
        Assert.NotEqual(firstHash, store.Hash);
    }
}
