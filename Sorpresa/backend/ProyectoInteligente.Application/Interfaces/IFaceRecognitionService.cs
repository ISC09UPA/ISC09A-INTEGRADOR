namespace LockerSmart.Application.Interfaces;

public interface IFaceRecognitionService
{
    Task<string> RegisterPersonAsync(string displayName, string imageSasUrl, CancellationToken cancellationToken = default);
    Task<string?> IdentifyPersonAsync(byte[] image, CancellationToken cancellationToken = default);
}
