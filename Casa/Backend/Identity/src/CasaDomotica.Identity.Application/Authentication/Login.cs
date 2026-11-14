using System.Security.Cryptography;
using System.Text;
using CasaDomotica.Identity.Application.Common;
using CasaDomotica.Identity.Domain.Entities;
using CasaDomotica.Identity.Domain.Interfaces;

namespace CasaDomotica.Identity.Application.Authentication;
public sealed class Login(IUserRepository users, ITokenService tokens, IDateTimeProvider clock, IPasswordHasher passwords, ISessionRepository sessions)
{
    private static readonly string[] Audiences = ["Identity.Public", "Home.Public", "Security.Public", "Notifications.Public", "Assistant.Public"];
    // Equal-cost verification for unknown accounts; generated once, not a usable account.
    private readonly string _dummyHash = passwords.Hash(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
    public async Task<Result<LoginResponse>> ExecuteAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || password.Length is < 1 or > 1024) return Invalid();
        var user = await users.FindByEmailAsync(email.Trim().ToUpperInvariant(), cancellationToken);
        var valid = passwords.Verify(user?.PasswordHash ?? _dummyHash, password);
        if (!valid || user is null || !user.Enabled) return Invalid();
        var now = clock.UtcNow;
        var session = new Session { Id = Guid.NewGuid(), UserId = user.Id, CreatedAt = now, ExpiresAt = now.AddDays(7) };
        var refresh = NewRefresh();
        // Signing may fail due to missing configuration: do it before persisting a new session.
        var access = tokens.GenerateAccessToken(user.Id, session.Id, Audiences);
        await sessions.CreateAsync(session, HashRefresh(refresh), cancellationToken);
        return new Result<LoginResponse>.Success(new(access, refresh, 300, 604800, session.Id, user.Id));
    }
    public async Task<Result<LoginResponse>> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) || refreshToken.Length > 256) return Invalid();
        var replacement = NewRefresh();
        var now = clock.UtcNow;
        var session = await sessions.RotateAsync(HashRefresh(refreshToken), HashRefresh(replacement), now, ct);
        if (session is null) return Invalid();
        var user = await users.FindByIdAsync(session.UserId, ct);
        if (user is null || !user.Enabled) { await sessions.RevokeByRefreshAsync(HashRefresh(replacement), ct); return Invalid(); }
        var access = tokens.GenerateAccessToken(user.Id, session.Id, Audiences);
        return new Result<LoginResponse>.Success(new(access, replacement, 300, Math.Max(0, (int)(session.ExpiresAt - now).TotalSeconds), session.Id, user.Id));
    }
    public Task LogoutAsync(string refreshToken, CancellationToken ct) => sessions.RevokeByRefreshAsync(HashRefresh(refreshToken), ct);
    private static Result<LoginResponse> Invalid() => new Result<LoginResponse>.Failure("invalid_credentials", "Invalid credentials.");
    public static string HashRefresh(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string NewRefresh() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
public sealed record LoginResponse(string AccessToken, string RefreshToken, int ExpiresIn, int RefreshExpiresIn, Guid SessionId, Guid UserId);
