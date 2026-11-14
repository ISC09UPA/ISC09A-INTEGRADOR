namespace CasaDomotica.Identity.Domain.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(Guid userId, Guid sessionId, IEnumerable<string> audiences);
    string GenerateServiceToken(string clientId, string scope);
}
