namespace CasaDomotica.Security.Domain;

public static class BiometricMethods
{
    public const string Face = "face";
    public const string Fingerprint = "fingerprint";

    public static bool IsValid(string? method) =>
        method is Face or Fingerprint;
}

public static class PersonStatuses
{
    public const string Pending = "pending";
    public const string Authorized = "authorized";
    public const string Revoked = "revoked";
    public const string Expired = "expired";
    public const string Failed = "failed";
}

public static class EnrollmentStatuses
{
    public const string Pending = "pending";
    public const string Acquiring = "acquiring";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";

    public static bool IsTerminal(string? status) =>
        status is Completed or Failed or Cancelled;
}

public static class BiometricPolicy
{
    public const double MinimumConfidence = 0.75;

    public static string EffectiveStatus(
        string storedStatus,
        DateTimeOffset validUntilUtc,
        DateTimeOffset? revokedAtUtc,
        DateTimeOffset now)
    {
        if (revokedAtUtc is not null || storedStatus == PersonStatuses.Revoked)
            return PersonStatuses.Revoked;

        if (storedStatus == PersonStatuses.Authorized && validUntilUtc <= now)
            return PersonStatuses.Expired;

        return storedStatus;
    }

    public static bool IsAccessAllowed(
        string storedStatus,
        DateTimeOffset validUntilUtc,
        DateTimeOffset? revokedAtUtc,
        DateTimeOffset? retentionPurgedAtUtc,
        double? confidence,
        DateTimeOffset now)
    {
        if (revokedAtUtc is not null || retentionPurgedAtUtc is not null)
            return false;

        if (storedStatus != PersonStatuses.Authorized)
            return false;

        if (validUntilUtc <= now)
            return false;

        return confidence is >= MinimumConfidence;
    }
}