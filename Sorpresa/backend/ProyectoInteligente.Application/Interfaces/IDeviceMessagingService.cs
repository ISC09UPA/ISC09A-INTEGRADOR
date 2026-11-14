using LockerSmart.Application.DTOs;

namespace LockerSmart.Application.Interfaces;

public interface IDeviceMessagingService
{
    Task EnviarComandoAsync(DeviceCommandMessage command, CancellationToken cancellationToken = default);
}
