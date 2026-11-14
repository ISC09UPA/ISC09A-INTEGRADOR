using System.Text.Json;
using CasaDomotica.Home.Domain.Interfaces;
using CasaDomotica.Home.Infrastructure.Runtime;
using Xunit;

namespace CasaDomotica.Home.Application.Tests;
public sealed class DeviceDispatcherTests
{
    private sealed class Clock : TimeProvider { public DateTimeOffset Now = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Queue(CommandRow row) : IDispatchQueue
    {
        public Action? OnClaim; public int Claims;
        public Task ExpireAsync(DateTimeOffset now, CancellationToken ct) { if (row.Status == "pending" && row.ExpiresAtUtc < now) { row.Status = row.DispatchedAtUtc is null ? "expired" : "unknown"; row.Active = row.Status == "unknown"; } return Task.CompletedTask; }
        public Task<CommandRow?> NextAsync(DateTimeOffset now, CancellationToken ct) => Task.FromResult(row.Status == "pending" && row.DispatchedAtUtc is null && row.ExpiresAtUtc > now ? row : null);
        public Task<bool> HasPendingAsync(CancellationToken ct) => Task.FromResult(row.Status == "pending");
        public Task<bool> ClaimAsync(string id, DateTimeOffset now, CancellationToken ct) { if (row.Status != "pending" || row.DispatchedAtUtc is not null || row.ExpiresAtUtc <= now) return Task.FromResult(false); Claims++; row.DispatchedAtUtc = now; OnClaim?.Invoke(); return Task.FromResult(true); }
        public Task CompleteAsync(CommandRow command, string status, string code, string reason, bool onlyIfUnclaimed, CancellationToken ct) { if (row.Status == "pending" && (!onlyIfUnclaimed || row.DispatchedAtUtc is null)) { row.Status = status; row.Active = status == "unknown"; } return Task.CompletedTask; }
    }
    private sealed class Transport : IDeviceTransport
    {
        public int Calls; public int Status = 202; public bool Fail; public string? Body;
        public Task<int> InvokeAsync(string deviceId, string methodName, string payload, CancellationToken ct) { Calls++; Body = payload; if (Fail) throw new IOException("isolated transport failure"); return Task.FromResult(Status); }
    }
    private sealed class Tickets(Clock clock, Action? duringPreparation = null) : ICaptureUploadTickets
    {
        public Task<CaptureUploadTicket> PrepareAsync(CommandRow command, CancellationToken ct)
        {
            duringPreparation?.Invoke();
            return Task.FromResult(new CaptureUploadTicket(Guid.NewGuid(), "https://example.invalid/upload", new string('A', 43), clock.Now.AddMinutes(5)));
        }
    }
    private sealed class Authorization(bool allowed = true, Action? duringCheck = null, bool revoked = false) : IUserAuthorizer
    {
        public string? Action;
        public Task<bool> CheckAsync(UserContext user, Guid houseId, string action, Guid correlationId, CancellationToken ct) { Action = action; duringCheck?.Invoke(); if (revoked) throw new HomeException("sessionRevoked", "revoked"); return Task.FromResult(allowed); }
    }
    private static CommandRow Door(Clock clock) => new() { CommandId = Guid.NewGuid().ToString(), HouseId = Guid.NewGuid(), LightId = "door", DeviceId = "device", Type = "OpenDoor", PayloadJson = "{\"doorId\":\"configured\",\"durationMs\":1000}", Sequence = 1, UserId = Guid.NewGuid(), SessionId = Guid.NewGuid(), CorrelationId = Guid.NewGuid(), RequestedAtUtc = clock.Now, ExpiresAtUtc = clock.Now.AddSeconds(10) };
    [Fact]
    public async Task AcceptedMethodRemainsPendingAndClaimPreventsResend()
    {
        var clock = new Clock(); var row = Door(clock); var queue = new Queue(row); var transport = new Transport(); var auth = new Authorization(); var dispatcher = new DeviceDispatcher(queue, transport, auth, clock);
        await dispatcher.DispatchNextAsync(default); await dispatcher.DispatchNextAsync(default);
        Assert.Equal("pending", row.Status); Assert.True(row.Active); Assert.Equal(1, transport.Calls); Assert.Equal("home:control:door", auth.Action);
        using var body = JsonDocument.Parse(transport.Body!); Assert.Equal(row.CommandId, body.RootElement.GetProperty("attemptId").GetString());
    }
    [Theory]
    [InlineData(400, false, "rejected", false)]
    [InlineData(500, false, "unknown", true)]
    [InlineData(0, true, "unknown", true)]
    public async Task RejectionAndAmbiguityHaveDifferentRetrySafety(int status, bool fail, string expected, bool active)
    {
        var clock = new Clock(); var row = Door(clock); var queue = new Queue(row); var transport = new Transport { Status = status, Fail = fail }; var dispatcher = new DeviceDispatcher(queue, transport, new Authorization(), clock);
        await dispatcher.DispatchNextAsync(default); await dispatcher.DispatchNextAsync(default);
        Assert.Equal(expected, row.Status); Assert.Equal(active, row.Active); Assert.Equal(1, transport.Calls);
    }
    [Fact]
    public async Task RevokedPermissionIsRejectedBeforeClaim()
    {
        var clock = new Clock(); var row = Door(clock); var queue = new Queue(row); var transport = new Transport();
        await new DeviceDispatcher(queue, transport, new Authorization(revoked: true), clock).DispatchNextAsync(default);
        Assert.Equal("rejected", row.Status); Assert.False(row.Active); Assert.Equal(0, queue.Claims); Assert.Equal(0, transport.Calls);
    }
    [Fact]
    public async Task AuthorizationFailureCannotUnlockAnOrderAlreadyClaimedByAnotherWorker()
    {
        var clock = new Clock(); var row = Door(clock); var queue = new Queue(row); var transport = new Transport();
        var auth = new Authorization(false, () => row.DispatchedAtUtc = clock.Now);
        await new DeviceDispatcher(queue, transport, auth, clock).DispatchNextAsync(default);
        Assert.Equal("pending", row.Status); Assert.True(row.Active); Assert.Equal(0, transport.Calls);
    }
    [Fact]
    public async Task DeadlineIsRecheckedAfterClaimBeforeTheNetworkCall()
    {
        var clock = new Clock(); var row = Door(clock); var queue = new Queue(row) { OnClaim = () => clock.Now = clock.Now.AddSeconds(11) }; var transport = new Transport();
        await new DeviceDispatcher(queue, transport, new Authorization(), clock).DispatchNextAsync(default);
        Assert.Equal("expired", row.Status); Assert.False(row.Active); Assert.Equal(0, transport.Calls);
    }
    [Fact]
    public async Task CaptureTicketIsSentOnlyAfterClaimAndTheDeadlineIsRechecked()
    {
        var clock = new Clock(); var row = Door(clock); row.Type = "CaptureImage"; row.PayloadJson = JsonSerializer.Serialize(new { captureId = Guid.NewGuid() });
        var queue = new Queue(row); var transport = new Transport();
        await new DeviceDispatcher(queue, transport, new Authorization(), clock, new Tickets(clock)).DispatchNextAsync(default);
        using var body = JsonDocument.Parse(transport.Body!);
        Assert.Equal(new string('A', 43), body.RootElement.GetProperty("payload").GetProperty("uploadToken").GetString());
        Assert.Equal(1, queue.Claims); Assert.Equal(1, transport.Calls);
        row = Door(clock); row.Type = "CaptureImage"; row.PayloadJson = JsonSerializer.Serialize(new { captureId = Guid.NewGuid() });
        queue = new Queue(row); transport = new Transport();
        await new DeviceDispatcher(queue, transport, new Authorization(), clock, new Tickets(clock, () => clock.Now = clock.Now.AddSeconds(11))).DispatchNextAsync(default);
        Assert.Equal("expired", row.Status); Assert.Equal(0, transport.Calls);
    }
}
