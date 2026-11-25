using CasaDomotica.Identity.Application.Accounts;
using CasaDomotica.Identity.Application.Authentication;
using CasaDomotica.Identity.Domain.Interfaces;
using CasaDomotica.Identity.Infrastructure.Persistence;
using CasaDomotica.Identity.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CasaDomotica.Identity.Application.Tests;

public sealed class AccountOperationsTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IdentityDb _db;
    private readonly FakeClock _clock = new();
    private readonly FakeEmailSender _emails = new();
    private readonly PasswordHasher _hasher = new();
    private readonly SqlIdentityRepository _identityRepo;
    private readonly AccountOperations _accounts;
    private readonly Login _login;
    private readonly AuthorizeUser _authz;
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _houseId = Guid.NewGuid();

    public AccountOperationsTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<IdentityDb>().UseSqlite(_connection).Options;
        _db = new IdentityDb(options);
        _db.Database.EnsureCreated();

        _db.Users.Add(new UserRow
        {
            Id = _adminId,
            Email = "admin@casa.test",
            NormalizedEmail = "ADMIN@CASA.TEST",
            PasswordHash = _hasher.Hash("Admin-Password-1234!"),
            Enabled = true,
            EmailVerified = true
        });
        _db.Houses.Add(new HouseRow { Id = _houseId, Name = "Casa Norte" });
        _db.Memberships.Add(new MembershipRow { UserId = _adminId, HouseId = _houseId, Role = "admin" });
        _db.SaveChanges();

        _identityRepo = new SqlIdentityRepository(_db);
        var store = new SqlAccountStore(_db);
        _accounts = new AccountOperations(store, _identityRepo, _hasher, _clock, _emails);
        _login = new Login(_identityRepo, new TestTokens(), _clock, _hasher, _identityRepo);
        _authz = new AuthorizeUser(_identityRepo, _identityRepo, _clock);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Admin_invites_new_resident_stores_only_hash_accepts_once_and_blocks_reuse()
    {
        var (created, createErr) = await _accounts.CreateInvitationAsync(_adminId, _houseId, "nuevo@casa.test", default);
        Assert.Null(createErr);
        Assert.NotNull(created);
        Assert.Equal("resident", created!.Invitation.Role);

        var row = await _db.Invitations.SingleAsync(x => x.Id == created.Invitation.Id);
        Assert.NotEqual(created.InvitationToken, row.TokenHash);
        Assert.Equal(64, row.TokenHash.Length);

        // Wrong target email is denied without consuming the invitation.
        var (_, wrongTarget) = await _accounts.AcceptInvitationAsync(created.InvitationToken, "otro@casa.test", "Residente-Pass-1234!", default);
        Assert.Equal("forbidden", wrongTarget?.Code);

        // New invitee defines their own password and joins as resident.
        var (accepted, acceptErr) = await _accounts.AcceptInvitationAsync(created.InvitationToken, "nuevo@casa.test", "Residente-Pass-1234!", default);
        Assert.Null(acceptErr);
        Assert.True(accepted!.CreatedNewUser);
        Assert.Equal("resident", accepted.Role);

        // Reusing the same token is rejected (single-use).
        var (_, reuseErr) = await _accounts.AcceptInvitationAsync(created.InvitationToken, "nuevo@casa.test", "Residente-Pass-1234!", default);
        Assert.Equal("invalidCredential", reuseErr?.Code);

        // New resident logs in and cannot administer members.
        var loginRes = Assert.IsType<Common.Result<LoginResponse>.Success>(await _login.ExecuteAsync("nuevo@casa.test", "Residente-Pass-1234!"));
        var (_, residentListErr) = await _accounts.ListMembersAsync(loginRes.Value.UserId, _houseId, default);
        Assert.Equal("forbidden", residentListErr?.Code);

        // Resident has home:read, not home:control:door.
        Assert.Equal(AuthorizationOutcome.Allowed, await _authz.EvaluateAsync(loginRes.Value.UserId, loginRes.Value.SessionId, _houseId, "home:read", default));
        Assert.Equal(AuthorizationOutcome.Denied, await _authz.EvaluateAsync(loginRes.Value.UserId, loginRes.Value.SessionId, _houseId, "home:control:door", default));

        // Admin revokes the resident; session and house authorization immediately fail.
        var removeErr = await _accounts.RemoveResidentAsync(_adminId, _houseId, loginRes.Value.UserId, default);
        Assert.Null(removeErr);
        Assert.Equal(AuthorizationOutcome.SessionRevoked, await _authz.EvaluateAsync(loginRes.Value.UserId, loginRes.Value.SessionId, _houseId, "home:read", default));
        Assert.False(await _identityRepo.IsActiveAsync(loginRes.Value.SessionId, loginRes.Value.UserId, _clock.UtcNow, default));
    }

    [Fact]
    public async Task Expired_and_revoked_invitations_are_rejected()
    {
        var (created, _) = await _accounts.CreateInvitationAsync(_adminId, _houseId, "expira@casa.test", default);
        _clock.UtcNow = _clock.UtcNow.AddHours(73);

        var (_, expiredErr) = await _accounts.AcceptInvitationAsync(created!.InvitationToken, "expira@casa.test", "Residente-Pass-1234!", default);
        Assert.Equal("requestExpired", expiredErr?.Code);

        _clock.UtcNow = DateTimeOffset.UtcNow;
        var (second, _) = await _accounts.CreateInvitationAsync(_adminId, _houseId, "revocada@casa.test", default);
        Assert.Null(await _accounts.RevokeInvitationAsync(_adminId, _houseId, second!.Invitation.Id, default));

        var (_, revokedErr) = await _accounts.AcceptInvitationAsync(second.InvitationToken, "revocada@casa.test", "Residente-Pass-1234!", default);
        Assert.Equal("invalidCredential", revokedErr?.Code);
    }

    [Fact]
    public async Task Existing_user_accepts_invitation_with_current_password_without_password_reset()
    {
        var existingId = Guid.NewGuid();
        var originalPass = "Mi-Clave-Original-9876!";
        _db.Users.Add(new UserRow
        {
            Id = existingId,
            Email = "existente@casa.test",
            NormalizedEmail = "EXISTENTE@CASA.TEST",
            PasswordHash = _hasher.Hash(originalPass),
            Enabled = true,
            EmailVerified = true
        });
        await _db.SaveChangesAsync();

        var (created, _) = await _accounts.CreateInvitationAsync(_adminId, _houseId, "existente@casa.test", default);

        // Wrong password does not link user or reset password.
        var (_, wrongPassErr) = await _accounts.AcceptInvitationAsync(created!.InvitationToken, "existente@casa.test", "Otra-Clave-Distinta-1234!", default);
        Assert.Equal("invalidCredential", wrongPassErr?.Code);

        // Current password links existing account without overwriting password.
        var (accepted, err) = await _accounts.AcceptInvitationAsync(created.InvitationToken, "existente@casa.test", originalPass, default);
        Assert.Null(err);
        Assert.False(accepted!.CreatedNewUser);
        Assert.IsType<Common.Result<LoginResponse>.Success>(await _login.ExecuteAsync("existente@casa.test", originalPass));
    }

    [Fact]
    public async Task Authenticated_password_change_revokes_active_sessions()
    {
        var session = Assert.IsType<Common.Result<LoginResponse>.Success>(await _login.ExecuteAsync("admin@casa.test", "Admin-Password-1234!"));
        Assert.True(await _identityRepo.IsActiveAsync(session.Value.SessionId, _adminId, _clock.UtcNow, default));

        var err = await _accounts.ChangePasswordAsync(_adminId, "Admin-Password-1234!", "Nueva-Clave-Admin-5678!", default);
        Assert.Null(err);

        Assert.False(await _identityRepo.IsActiveAsync(session.Value.SessionId, _adminId, _clock.UtcNow, default));
        Assert.IsType<Common.Result<LoginResponse>.Failure>(await _login.ExecuteAsync("admin@casa.test", "Admin-Password-1234!"));
        Assert.IsType<Common.Result<LoginResponse>.Success>(await _login.ExecuteAsync("admin@casa.test", "Nueva-Clave-Admin-5678!"));
    }

    [Fact]
    public async Task Password_reset_prevents_enumeration_rejects_unverified_and_revokes_sessions()
    {
        var unverifiedId = Guid.NewGuid();
        _db.Users.Add(new UserRow
        {
            Id = unverifiedId,
            Email = "noverificado@casa.test",
            NormalizedEmail = "NOVERIFICADO@CASA.TEST",
            PasswordHash = _hasher.Hash("NoVerificado-Pass-1234!"),
            Enabled = true,
            EmailVerified = false
        });
        await _db.SaveChangesAsync();

        // Unknown and unverified accounts both return uniform null (202 accepted) without sending emails.
        Assert.Null(await _accounts.RequestPasswordResetAsync("nadie@casa.test", default));
        Assert.Null(await _accounts.RequestPasswordResetAsync("noverificado@casa.test", default));
        Assert.Empty(_emails.Resets);

        // Verified admin logs in, requests reset, and receives a single-use token.
        var session = Assert.IsType<Common.Result<LoginResponse>.Success>(await _login.ExecuteAsync("admin@casa.test", "Admin-Password-1234!"));
        Assert.Null(await _accounts.RequestPasswordResetAsync("admin@casa.test", default));
        var dispatched = Assert.Single(_emails.Resets);

        // Confirm reset changes password and revokes active sessions.
        Assert.Null(await _accounts.ConfirmPasswordResetAsync(dispatched.Token, "admin@casa.test", "Recuperada-Pass-2026!", default));
        Assert.False(await _identityRepo.IsActiveAsync(session.Value.SessionId, _adminId, _clock.UtcNow, default));

        // Token cannot be reused.
        var reuse = await _accounts.ConfirmPasswordResetAsync(dispatched.Token, "admin@casa.test", "Otro-Intento-2026!", default);
        Assert.Equal("invalidCredential", reuse?.Code);
    }

    private sealed class FakeClock : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    }

    private sealed class FakeEmailSender : IAccountEmailSender
    {
        public List<(string Email, string Token)> Resets { get; } = [];
        public Task SendInvitationAsync(string recipientEmail, string houseName, string token, DateTimeOffset expiresAtUtc, CancellationToken ct) => Task.CompletedTask;
        public Task SendPasswordResetAsync(string recipientEmail, string token, DateTimeOffset expiresAtUtc, CancellationToken ct)
        {
            Resets.Add((recipientEmail, token));
            return Task.CompletedTask;
        }
    }

    private sealed class TestTokens : ITokenService
    {
        public string GenerateAccessToken(Guid userId, Guid sessionId, IEnumerable<string> audiences) => "test-jwt";
        public string GenerateServiceToken(string clientId, string scope) => throw new NotSupportedException();
    }
}