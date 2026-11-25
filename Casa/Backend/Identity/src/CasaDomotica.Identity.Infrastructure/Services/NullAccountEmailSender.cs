using CasaDomotica.Identity.Domain.Interfaces;

namespace CasaDomotica.Identity.Infrastructure.Services;

public sealed class NullAccountEmailSender : IAccountEmailSender
{
    // Real email delivery is never triggered automatically in local/offline environments.
    public Task SendInvitationAsync(string recipientEmail, string houseName, string token, DateTimeOffset expiresAtUtc, CancellationToken ct) =>
        Task.CompletedTask;

    public Task SendPasswordResetAsync(string recipientEmail, string token, DateTimeOffset expiresAtUtc, CancellationToken ct) =>
        Task.CompletedTask;
}