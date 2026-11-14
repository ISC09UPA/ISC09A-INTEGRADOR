using System.Text.Json;
using CasaDomotica.Home.Domain.Interfaces;
using CasaDomotica.Home.Infrastructure.Runtime;
using Xunit;

namespace CasaDomotica.Home.Application.Tests;
public sealed class LightStateProjectionTests
{
    private static JsonElement Snapshot(bool on, string mode = "manual") => JsonSerializer.SerializeToElement(new { lightOn = on, lightingMode = mode, appliedAtUtc = "2026-11-13T12:00:00Z" });
    [Fact]
    public void FreshnessRequiresAReportForTheCurrentRevision()
    {
        var light = new LightRow(); var start = DateTimeOffset.Parse("2026-11-13T12:00:00Z");
        LightStateProjection.Apply(light, Snapshot(true), 4, start);
        LightStateProjection.Apply(light, Snapshot(true), 4, start.AddSeconds(30));
        Assert.Equal(start.AddSeconds(30), light.StateReportedAtUtc);
        Assert.False(LightStateProjection.Apply(light, Snapshot(false), 3, start.AddMinutes(2)));
        Assert.True(light.LightOn); Assert.Equal(start.AddSeconds(30), light.StateReportedAtUtc);
        LightStateProjection.Apply(light, Snapshot(false), 5, start.AddSeconds(10));
        Assert.False(light.LightOn); Assert.Equal(start.AddSeconds(10), light.StateReportedAtUtc);
    }
    [Fact]
    public void EqualRevisionCannotChangeTheAppliedState()
    {
        var light = new LightRow(); var now = DateTimeOffset.UtcNow;
        LightStateProjection.Apply(light, Snapshot(true), 1, now);
        var error = Assert.Throws<HomeException>(() => LightStateProjection.Apply(light, Snapshot(false), 1, now.AddSeconds(10)));
        Assert.Equal("invalidEvent", error.Code); Assert.True(light.LightOn); Assert.Equal(now, light.StateReportedAtUtc);
    }
}
