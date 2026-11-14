using System.Text.Json;
using CasaDomotica.Home.Domain.Interfaces;
namespace CasaDomotica.Home.Infrastructure.Runtime;

public static class LightStateProjection
{
    public static bool Apply(LightRow light, JsonElement snapshot, long revision, DateTimeOffset reportedAt, string? commandId = null)
    {
        var on = snapshot.GetProperty("lightOn").GetBoolean();
        var mode = snapshot.GetProperty("lightingMode").GetString();
        if (light.AppliedAtUtc is not null && revision == light.StateRevision && (light.LightOn != on || light.LightingMode != mode)) throw new HomeException("invalidEvent", "A state revision cannot describe different states.");
        if (light.AppliedAtUtc is not null && revision < light.StateRevision) return false;
        if (light.AppliedAtUtc is null || revision > light.StateRevision)
        {
            light.StateRevision = revision; light.LightOn = on; light.LightingMode = mode;
            light.AppliedAtUtc = snapshot.GetProperty("appliedAtUtc").GetDateTimeOffset();
            light.StateReportedAtUtc = reportedAt;
            if (commandId is not null) light.LastCommandId = commandId;
        }
        else if (light.StateReportedAtUtc is null || reportedAt > light.StateReportedAtUtc) light.StateReportedAtUtc = reportedAt;
        return true;
    }
}
