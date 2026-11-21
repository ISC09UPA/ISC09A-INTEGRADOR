using System.Text.Json;
using CasaDomotica.Security.Domain;
using Xunit;

public class SecurityProjectionTests
{
    private static HouseEvent Event(string type, string payload, int age = 0) => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "device", type, DateTimeOffset.UtcNow.AddSeconds(-age),
        DateTimeOffset.UtcNow, JsonDocument.Parse(payload).RootElement.Clone(), true);

    [Theory]
    [InlineData("armed", true)]
    [InlineData("disarmed", false)]
    public void MotionIsEvaluatedUsingTheModeAtTheTimeOfTheEvent(string mode, bool incident)
    {
        var change = SecurityProjection.Apply(Event("MotionDetected", $$"""{"roomId":"sala","securityModeApplied":"{{mode}}"}""", 60));
        Assert.Equal(incident, change.Incident is not null);
        Assert.Equal(mode, change.Activity!.Result);
    }

    [Fact]
    public void LateModeNeverReplacesNewerObservation()
    {
        var old = SecurityProjection.Apply(Event("DeviceStateChanged", """{"securityModeApplied":"armed"}""", 60)).Mode!;
        var recent = SecurityProjection.Apply(Event("DeviceStateChanged", """{"securityModeApplied":"disarmed"}""", 1)).Mode!;
        Assert.False(SecurityProjection.IsNewer(old, recent));
        Assert.True(SecurityProjection.IsNewer(recent, old));
    }

    [Fact]
    public void InvalidModeAndMissingAttemptNeverBecomeIncidents()
    {
        Assert.Throws<InvalidDataException>(() => SecurityProjection.Apply(Event("MotionDetected", """{"roomId":"sala","securityModeApplied":"unknown"}""")));
        Assert.Throws<InvalidDataException>(() => SecurityProjection.Apply(Event("AccessRequested", """{"roomId":"sala"}""")));
    }

    [Fact]
    public void ResultWithoutModeDoesNotInventAnEffectiveMode() =>
        Assert.Null(SecurityProjection.Apply(Event("DeviceStateChanged", """{"status":"executed"}""")).Mode);

    [Fact]
    public void UnmappedDeviceCannotSetEffectiveSecurityMode() =>
        Assert.Null(SecurityProjection.Apply(Event("DeviceStateChanged", """{"securityModeApplied":"armed"}""") with { IsSecurityController = false }).Mode);

    [Fact]
    public void DoorResultIsRecordedWithoutPretendingTheDoorPositionIsKnown()
    {
        var change = SecurityProjection.Apply(Event("DeviceStateChanged", """{"type":"OpenDoor","status":"executed"}"""));
        Assert.Equal("access", change.Activity!.Type);
        Assert.Equal("executed", change.Activity.Result);
        Assert.Null(change.Incident);
        Assert.Null(change.Mode);
    }
}
