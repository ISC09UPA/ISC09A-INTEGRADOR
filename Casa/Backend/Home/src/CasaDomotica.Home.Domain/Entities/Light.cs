namespace CasaDomotica.Home.Domain.Entities;

public sealed class Light
{
    public string Id { get; init; } = string.Empty;
    public string HouseId { get; init; } = string.Empty;
    public bool IsOn { get; private set; }
    public string Mode { get; private set; } = "manual";
    public long StateRevision { get; private set; }

    public void ApplyState(bool isOn, string mode, long stateRevision)
    {
        if (stateRevision < StateRevision)
        {
            // Ignore older events; projection does not roll back.
            return;
        }

        IsOn = isOn;
        Mode = mode;
        StateRevision = stateRevision;
    }
}
