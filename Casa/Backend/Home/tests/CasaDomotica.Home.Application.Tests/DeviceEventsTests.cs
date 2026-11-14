using System.Text.Json;
using CasaDomotica.Home.Application.Commands;
using CasaDomotica.Home.Domain.Interfaces;
using Xunit;
namespace CasaDomotica.Home.Application.Tests;
public sealed class DeviceEventsTests
{
    private static JsonElement Event(string type, object payload) => JsonSerializer.SerializeToElement(new { schemaVersion = 1, type, eventId = Guid.NewGuid(), correlationId = Guid.NewGuid(), occurredAtUtc = DateTimeOffset.UtcNow, payload });
    [Fact]
    public void Trusted_metadata_cannot_be_supplied_inside_the_device_envelope()
    {
        var forged = JsonSerializer.SerializeToElement(new { schemaVersion = 1, type = "TelemetryReported", eventId = Guid.NewGuid(), correlationId = Guid.NewGuid(), occurredAtUtc = DateTimeOffset.UtcNow, sourceDeviceId = "another-house-device", payload = new { lightPercent = 50, motion = false, lightOn = false, securityModeApplied = "disarmed" } });
        Assert.Throws<HomeException>(() => DeviceEvents.Parse(forged));
    }
    [Fact]
    public void A_pending_ack_is_not_a_device_execution_result()
    {
        var message = Event("CommandResult", new { commandId = "order", type = "SetLight", status = "pending", reasonCode = (string?)null });
        Assert.Throws<HomeException>(() => DeviceEvents.Parse(message));
    }
    [Fact]
    public void Simulated_snapshots_are_not_accepted_as_real_hardware_results()
    {
        var message = Event("CommandResult", new { commandId = "order", type = "SetLight", status = "executed", reasonCode = (string?)null, stateRevision = 1, snapshot = new { lightOn = true, lightingMode = "manual", source = "simulated", appliedAtUtc = DateTimeOffset.UtcNow } });
        Assert.Throws<HomeException>(() => DeviceEvents.Parse(message));
    }
    [Fact]
    public void Motion_retains_the_security_mode_at_the_time_it_happened()
    {
        var message = DeviceEvents.Parse(Event("MotionDetected", new { roomId = "room", securityModeApplied = "disarmed" }));
        Assert.Equal("disarmed", message.Payload.GetProperty("securityModeApplied").GetString());
    }
}
