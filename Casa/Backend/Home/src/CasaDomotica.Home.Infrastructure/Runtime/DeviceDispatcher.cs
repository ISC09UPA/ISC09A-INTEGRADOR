using System.Text.Json;
using Azure.Identity;
using CasaDomotica.Home.Domain.Interfaces;
using Microsoft.Azure.Devices;
using Microsoft.Extensions.Configuration;
namespace CasaDomotica.Home.Infrastructure.Runtime;

public interface IDeviceTransport
{
    Task<int> InvokeAsync(string deviceId, string methodName, string payload, CancellationToken ct);
}
public sealed class IoTConnection : IDeviceTransport, IDisposable
{
    private readonly ServiceClient _client;
    public IoTConnection(IConfiguration configuration)
    {
        var host = configuration["IoT:HostName"] ?? throw new InvalidOperationException("Configure the trusted IoT Hub hostname.");
        _client = ServiceClient.Create(host, new DefaultAzureCredential(), TransportType.Amqp_WebSocket_Only);
    }
    public async Task<int> InvokeAsync(string deviceId, string methodName, string payload, CancellationToken ct)
    {
        var method = new CloudToDeviceMethod(methodName, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(5));
        method.SetPayloadJson(payload);
        return (await _client.InvokeDeviceMethodAsync(deviceId, method, ct)).Status;
    }
    public void Dispose() => _client.Dispose();
}

public interface IDispatchQueue
{
    Task ExpireAsync(DateTimeOffset now, CancellationToken ct);
    Task<CommandRow?> NextAsync(DateTimeOffset now, CancellationToken ct);
    Task<bool> HasPendingAsync(CancellationToken ct);
    Task<bool> ClaimAsync(string id, DateTimeOffset now, CancellationToken ct);
    Task CompleteAsync(CommandRow command, string status, string code, string reason, bool onlyIfUnclaimed, CancellationToken ct);
}

public sealed class DeviceDispatcher(IDispatchQueue queue, IDeviceTransport transport, IUserAuthorizer authorizer, TimeProvider clock, ICaptureUploadTickets? captureTickets = null, IVideoProducerTickets? videoTickets = null) : IDeviceDispatcher
{
    public async Task<bool> DispatchNextAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await queue.ExpireAsync(now, ct);
        var command = await queue.NextAsync(now, ct);
        if (command is null) return await queue.HasPendingAsync(ct);
        var action = command.Type switch { "SetLight" or "SetLightingMode" => "home:control:lights", "SetSecurityMode" => "home:control:security", "OpenDoor" => "home:control:door", "CaptureImage" or "StartVideo" => "home:control:capture", _ => null };
        var allowed = false;
        try { if (action is not null) allowed = await authorizer.CheckAsync(new(command.UserId, command.SessionId), command.HouseId, action, command.CorrelationId, ct); }
        catch (HomeException error) when (error.Code == "sessionRevoked") { }
        if (!allowed)
        {
            // A different worker may have claimed the row while authorization was in flight.
            await queue.CompleteAsync(command, "rejected", "forbidden", "Permission was revoked before dispatch.", true, ct); return true;
        }
        // A successful atomic claim is the durable boundary. Never resend a claimed row.
        if (!await queue.ClaimAsync(command.CommandId, clock.GetUtcNow(), ct)) return true;
        ct.ThrowIfCancellationRequested();
        if (clock.GetUtcNow() >= command.ExpiresAtUtc)
        {
            await queue.CompleteAsync(command, "expired", "expired", "The order expired before the network call.", false, ct); return true;
        }
        using var payload = JsonDocument.Parse(command.PayloadJson);
        object commandPayload = payload.RootElement;
        try
        {
            if (command.Type == "CaptureImage")
            {
                // The random upload secret is stored only as a hash and is delivered once
                // to the authenticated IoT device after the durable dispatch claim.
                var ticket = await (captureTickets ?? throw new HomeException("unavailable", "Capture upload service is not configured.")).PrepareAsync(command, ct);
                commandPayload = new { captureId = ticket.CaptureId, uploadUrl = ticket.Url, uploadToken = ticket.Token, uploadExpiresAtUtc = ticket.ExpiresAtUtc };
            }
            else if (command.Type == "StartVideo")
            {
                var ticket = await (videoTickets ?? throw new HomeException("unavailable", "Video producer service is not configured.")).PrepareAsync(command, ct);
                commandPayload = new { sessionId = ticket.SessionId, uploadUrl = ticket.UploadUrl, uploadToken = ticket.UploadToken, uploadExpiresAtUtc = ticket.ExpiresAtUtc };
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            // The durable claim happened, but no device call was made. Mark the order
            // failed instead of leaving it pending with a dispatch timestamp forever.
            await queue.CompleteAsync(command, "failed", "failed", "The device ticket could not be prepared before dispatch.", false, ct);
            return true;
        }
        if (clock.GetUtcNow() >= command.ExpiresAtUtc)
        {
            await queue.CompleteAsync(command, "expired", "expired", "The order expired before the network call.", false, ct); return true;
        }
        var body = JsonSerializer.Serialize(new { commandId = command.CommandId, deviceId = command.DeviceId, type = command.Type, commandSequence = command.Sequence, attemptId = command.Type == "OpenDoor" ? command.CommandId : null, issuedAtUtc = command.RequestedAtUtc, expiresAtUtc = command.ExpiresAtUtc, payload = commandPayload });
        try
        {
            var status = await transport.InvokeAsync(command.DeviceId, command.Type, body, ct);
            // 202 is queue admission only; a D2C event must confirm execution.
            if (status is 400 or 429) await queue.CompleteAsync(command, "rejected", "deviceRejected", "The device did not accept the request.", false, ct);
            else if (status != 202) await queue.CompleteAsync(command, "unknown", "unknown", "The device returned an unconfirmed response.", false, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { await queue.CompleteAsync(command, "unknown", "unknown", "The IoT call ended without a confirmed outcome.", false, ct); }
        return true;
    }
}
