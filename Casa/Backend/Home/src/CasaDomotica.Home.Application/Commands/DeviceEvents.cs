using System.Text.Json;
using CasaDomotica.Home.Domain.Interfaces;
namespace CasaDomotica.Home.Application.Commands;
public sealed record DeviceEvent(Guid EventId, Guid CorrelationId, string Type, DateTimeOffset OccurredAtUtc, JsonElement Payload);
public static class DeviceEvents
{
    private static readonly HashSet<string> Kinds = ["TelemetryReported", "MotionDetected", "CaptureUploaded", "AccessRequested", "CommandResult"];
    private static readonly HashSet<string> Results = ["executed", "rejected", "expired", "unknown", "failed"];
    public static DeviceEvent Parse(JsonElement root)
    {
        try
        {
            Exact(root, ["schemaVersion", "type", "eventId", "occurredAtUtc", "correlationId", "payload"]);
            var schema = root.GetProperty("schemaVersion").GetInt32();
            var type = root.GetProperty("type").GetString()!;
            if (!(schema == 1 && Kinds.Contains(type) || schema == 2 && type == "DeviceStateReported")) Invalid();
            var id = root.GetProperty("eventId").GetGuid(); var correlation = root.GetProperty("correlationId").GetGuid();
            var occurred = root.GetProperty("occurredAtUtc").GetDateTimeOffset();
            if (occurred.Offset != TimeSpan.Zero || occurred > DateTimeOffset.UtcNow.AddSeconds(30)) Invalid();
            var payload = root.GetProperty("payload");
            if (payload.ValueKind != JsonValueKind.Object) Invalid();
            switch (type)
            {
                case "DeviceStateReported":
                    Exact(payload, ["lightId", "snapshot", "stateRevision", "securityModeApplied"]);
                    String(payload, "lightId", 128); SecurityMode(payload);
                    if (payload.GetProperty("stateRevision").GetInt64() < 0) Invalid();
                    var state = payload.GetProperty("snapshot");
                    Exact(state, ["lightOn", "lightingMode", "source", "appliedAtUtc"]);
                    _ = state.GetProperty("lightOn").GetBoolean();
                    if (state.GetProperty("lightingMode").GetString() is not ("auto" or "manual") || state.GetProperty("source").GetString() != "device" || state.GetProperty("appliedAtUtc").GetDateTimeOffset().Offset != TimeSpan.Zero || state.GetProperty("appliedAtUtc").GetDateTimeOffset() > occurred.AddSeconds(5)) Invalid();
                    break;
                case "TelemetryReported":
                    Exact(payload, ["lightPercent", "motion", "lightOn", "securityModeApplied"]);
                    if (!payload.GetProperty("lightPercent").TryGetDouble(out var percent) || !double.IsFinite(percent) || percent is < 0 or > 100) Invalid();
                    _ = payload.GetProperty("motion").GetBoolean(); _ = payload.GetProperty("lightOn").GetBoolean(); SecurityMode(payload); break;
                case "MotionDetected":
                    Exact(payload, ["roomId", "securityModeApplied", "configurationVersion"]);
                    String(payload, "roomId", 128); SecurityMode(payload);
                    if (payload.TryGetProperty("configurationVersion", out var version) && (!version.TryGetInt64(out var number) || number < 0)) Invalid();
                    break;
                case "CommandResult":
                    Exact(payload, ["commandId", "status", "type", "reasonCode", "reason", "snapshot", "stateRevision", "eventId", "correlationId"]);
                    String(payload, "commandId", 128);
                    var commandType = payload.GetProperty("type").GetString();
                    if (commandType is not ("SetLight" or "SetLightingMode" or "SetSecurityMode" or "OpenDoor" or "CaptureImage" or "StartVideo")) Invalid();
                    var status = payload.GetProperty("status").GetString()!;
                    if (!Results.Contains(status)) Invalid();
                    if (payload.TryGetProperty("eventId", out var pid) && pid.GetGuid() != id || payload.TryGetProperty("correlationId", out var pc) && pc.GetGuid() != correlation) Invalid();
                    if (status == "executed")
                    {
                        if (payload.GetProperty("reasonCode").ValueKind != JsonValueKind.Null || payload.TryGetProperty("reason", out var reason) && reason.ValueKind != JsonValueKind.Null) Invalid();
                        var snapshot = payload.GetProperty("snapshot");
                        if (commandType == "StartVideo")
                        {
                            if (snapshot.ValueKind != JsonValueKind.Null || payload.GetProperty("stateRevision").ValueKind != JsonValueKind.Null) Invalid();
                        }
                        else
                        {
                            Exact(snapshot, ["lightOn", "lightingMode", "source", "appliedAtUtc"]);
                            _ = snapshot.GetProperty("lightOn").GetBoolean();
                            if (snapshot.GetProperty("lightingMode").GetString() is not ("auto" or "manual") || snapshot.GetProperty("source").GetString() != "device" || (snapshot.GetProperty("appliedAtUtc").GetDateTimeOffset().Offset != TimeSpan.Zero || snapshot.GetProperty("appliedAtUtc").GetDateTimeOffset() > occurred.AddSeconds(5)) || payload.GetProperty("stateRevision").GetInt64() < 0) Invalid();
                        }
                    }
                    else
                    {
                        if (payload.TryGetProperty("snapshot", out var snapshot) && snapshot.ValueKind != JsonValueKind.Null || payload.TryGetProperty("stateRevision", out var revision) && revision.ValueKind != JsonValueKind.Null) Invalid();
                        var code = payload.GetProperty("reasonCode").GetString();
                        var valid = status switch { "expired" => code == "expired", "failed" => code == "failed", "unknown" => code is "unknown" or "failed", "rejected" => code is "invalidPayload" or "unauthorized" or "forbidden" or "duplicateChanged" or "sequenceRejected" or "deviceRejected", _ => false };
                        if (!valid) Invalid();
                    }
                    break;
                case "CaptureUploaded":
                    _ = payload.GetProperty("captureId").GetGuid(); String(payload, "blobName", 1024); break;
                case "AccessRequested":
                    _ = payload.GetProperty("attemptId").GetGuid(); String(payload, "roomId", 128); break;
            }
            return new(id, correlation, type, occurred, payload.Clone());
        }
        catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException or FormatException or OverflowException) { throw new HomeException("invalidEvent", "The event does not satisfy the device protocol."); }
    }
    private static void SecurityMode(JsonElement payload) { if (payload.GetProperty("securityModeApplied").GetString() is not ("armed" or "disarmed")) Invalid(); }
    private static void String(JsonElement payload, string property, int max) { var value = payload.GetProperty(property).GetString(); if (string.IsNullOrWhiteSpace(value) || value.Length > max) Invalid(); }
    private static void Exact(JsonElement value, string[] allowed) { if (value.ValueKind != JsonValueKind.Object || (value.EnumerateObject().Any(property => !allowed.Contains(property.Name)) || value.EnumerateObject().Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != value.EnumerateObject().Count())) Invalid(); }
    private static void Invalid() => throw new HomeException("invalidEvent", "The event does not satisfy the device protocol.");
}
