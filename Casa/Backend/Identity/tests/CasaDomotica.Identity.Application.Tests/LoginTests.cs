using CasaDomotica.Identity.Application.Authentication;
using CasaDomotica.Identity.Domain.Entities;
using CasaDomotica.Identity.Domain.Interfaces;
using CasaDomotica.Identity.Infrastructure.Security;
using Xunit;
namespace CasaDomotica.Identity.Application.Tests;
public sealed class LoginTests
{
    private sealed class Users(User? value) : IUserRepository
    {
        public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) => Task.FromResult(value);
        public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(value);
    }
    private sealed class Tokens : ITokenService
    {
        public int Issued;
        public string[] Audiences = [];
        public string GenerateAccessToken(Guid userId, Guid sessionId, IEnumerable<string> audiences) { Issued++; Audiences = audiences.ToArray(); return "isolated-test-token"; }
        public string GenerateServiceToken(string clientId, string scope) => throw new NotSupportedException();
    }
    private sealed class Clock : IDateTimeProvider { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
    private sealed class Sessions : ISessionRepository
    {
        public int Created;
        public Task CreateAsync(Session session, string refreshHash, CancellationToken ct) { Created++; Assert.DoesNotContain("refresh-placeholder", refreshHash); Assert.Equal(64, refreshHash.Length); return Task.CompletedTask; }
        public Task<Session?> RotateAsync(string refreshHash, string replacementHash, DateTimeOffset now, CancellationToken ct) => Task.FromResult<Session?>(null);
        public Task RevokeByRefreshAsync(string refreshHash, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> IsActiveAsync(Guid sessionId, Guid userId, DateTimeOffset now, CancellationToken ct) => Task.FromResult(false);
    }
    [Fact]
    public async Task Unknown_account_cannot_obtain_a_session()
    {
        var tokens = new Tokens(); var sessions = new Sessions();
        var login = new Login(new Users(null), tokens, new Clock(), new PasswordHasher(), sessions);
        Assert.IsType<Common.Result<LoginResponse>.Failure>(await login.ExecuteAsync("unknown@example.test", "wrong-password"));
        Assert.Equal(0, tokens.Issued); Assert.Equal(0, sessions.Created);
    }
    [Fact]
    public async Task A_nonempty_wrong_password_is_rejected_for_an_existing_account()
    {
        var hasher = new PasswordHasher(); var tokens = new Tokens(); var sessions = new Sessions();
        var user = new User { Id = Guid.NewGuid(), Email = "test@example.test", PasswordHash = hasher.Hash("correct-application-password") };
        var login = new Login(new Users(user), tokens, new Clock(), hasher, sessions);
        Assert.IsType<Common.Result<LoginResponse>.Failure>(await login.ExecuteAsync(user.Email, "not-the-password"));
        Assert.Equal(0, tokens.Issued); Assert.Equal(0, sessions.Created);
    }
    [Fact]
    public async Task Valid_credentials_create_a_durable_session_and_random_refresh_token()
    {
        var hasher = new PasswordHasher(); var sessions = new Sessions();
        var user = new User { Id = Guid.NewGuid(), Email = "test@example.test", PasswordHash = hasher.Hash("correct-application-password") };
        var tokens = new Tokens(); var login = new Login(new Users(user), tokens, new Clock(), hasher, sessions);
        var result = Assert.IsType<Common.Result<LoginResponse>.Success>(await login.ExecuteAsync(user.Email, "correct-application-password"));
        Assert.Equal(1, sessions.Created); Assert.Equal(64, result.Value.RefreshToken.Length); Assert.Equal(300, result.Value.ExpiresIn);
        Assert.Equal(new[] { "Identity.Public", "Home.Public", "Security.Public", "Notifications.Public", "Assistant.Public" }, tokens.Audiences);
        Assert.NotEqual(result.Value.RefreshToken, Login.HashRefresh(result.Value.RefreshToken));
    }
    [Fact]
    public void Password_hashes_use_distinct_salts_and_reject_malformed_values()
    {
        var hasher = new PasswordHasher(); var first = hasher.Hash("application-password"); var second = hasher.Hash("application-password");
        Assert.NotEqual(first, second); Assert.True(hasher.Verify(first, "application-password")); Assert.False(hasher.Verify(first, "other")); Assert.False(hasher.Verify("malformed", "application-password"));
    }
}
