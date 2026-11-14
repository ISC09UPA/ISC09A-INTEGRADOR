using CasaDomotica.Identity.Application.Authentication;
using CasaDomotica.Identity.Domain.Entities;
using CasaDomotica.Identity.Domain.Interfaces;
using CasaDomotica.Identity.Domain.ValueObjects;
using Xunit;

namespace CasaDomotica.Identity.Application.Tests;
public sealed class AuthorizationTests
{
    private readonly Guid _userId = Guid.NewGuid(), _sessionId = Guid.NewGuid(), _houseId = Guid.NewGuid();
    private sealed class Users(User? user) : IUserRepository
    {
        public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) => Task.FromResult(user);
        public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(user?.Id == id ? user : null);
    }
    private sealed class Sessions(bool active) : ISessionRepository
    {
        public Task CreateAsync(Session session, string refreshHash, CancellationToken ct) => throw new NotSupportedException();
        public Task<Session?> RotateAsync(string refreshHash, string replacementHash, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
        public Task RevokeByRefreshAsync(string refreshHash, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> IsActiveAsync(Guid sessionId, Guid userId, DateTimeOffset now, CancellationToken ct) => Task.FromResult(active);
    }
    private sealed class Clock : IDateTimeProvider { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
    private AuthorizeUser Create(string role, bool active = true, bool enabled = true) => new(new Users(new User { Id = _userId, Enabled = enabled, Memberships = [new HouseMembership(_houseId, role)] }), new Sessions(active), new Clock());
    [Theory]
    [InlineData("home:read")][InlineData("home:control:lights")][InlineData("security:read")][InlineData("notifications:read")][InlineData("assistant:read")]
    public async Task MembersMayReadAndControlLights(string action)
    {
        Assert.Equal(AuthorizationOutcome.Allowed, await Create("resident").EvaluateAsync(_userId, _sessionId, _houseId, action, default));
        Assert.Equal(AuthorizationOutcome.Allowed, await Create("admin").EvaluateAsync(_userId, _sessionId, _houseId, action, default));
    }
    [Theory]
    [InlineData("home:control:door")][InlineData("home:control:security")][InlineData("home:control:capture")][InlineData("security:manage")][InlineData("notifications:manage")][InlineData("identity:manage:members")]
    public async Task SensitiveActionsRequireAdministrator(string action)
    {
        Assert.Equal(AuthorizationOutcome.Allowed, await Create("admin").EvaluateAsync(_userId, _sessionId, _houseId, action, default));
        Assert.Equal(AuthorizationOutcome.Denied, await Create("resident").EvaluateAsync(_userId, _sessionId, _houseId, action, default));
    }
    [Fact]
    public async Task DeniesOtherHousesUnknownActionsDisabledUsersAndRevokedSessions()
    {
        Assert.Equal(AuthorizationOutcome.Denied, await Create("admin").EvaluateAsync(_userId, _sessionId, Guid.NewGuid(), "home:read", default));
        Assert.Equal(AuthorizationOutcome.Denied, await Create("admin").EvaluateAsync(_userId, _sessionId, _houseId, "security:everything", default));
        Assert.Equal(AuthorizationOutcome.Denied, await Create("admin", enabled: false).EvaluateAsync(_userId, _sessionId, _houseId, "home:read", default));
        Assert.Equal(AuthorizationOutcome.SessionRevoked, await Create("admin", active: false).EvaluateAsync(_userId, _sessionId, _houseId, "home:read", default));
        Assert.Equal(AuthorizationOutcome.Denied, await Create("unknown").EvaluateAsync(_userId, _sessionId, _houseId, "home:read", default));
    }
    [Theory]
    [InlineData("home", "home:control:door", true)]
    [InlineData("home", "security:manage", false)]
    [InlineData("security", "security:manage", true)]
    [InlineData("security", "home:control:door", false)]
    [InlineData("assistant", "assistant:read", true)]
    [InlineData("assistant", "notifications:manage", false)]
    [InlineData("notifications", "notifications:manage", true)]
    [InlineData("unknown", "home:read", false)]
    public void ServiceActionsAreIsolated(string client, string action, bool expected) => Assert.Equal(expected, ServiceAuthorization.CanCheck(client, action));
}
