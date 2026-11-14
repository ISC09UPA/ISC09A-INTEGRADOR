namespace CasaDomotica.Identity.Application.Authentication;

/// <summary>Limits each authenticated service to the user actions it owns.</summary>
public static class ServiceAuthorization
{
    public static bool CanCheck(string? client, string? action) => client switch
    {
        "home" => action is "home:read" or "home:control:lights" or "home:control:security" or "home:control:door" or "home:control:capture",
        "security" => action is "security:read" or "security:manage",
        "notifications" => action is "notifications:read" or "notifications:manage",
        "assistant" => action is "assistant:read",
        _ => false
    };
    public static bool IsKnownClient(string? client) => client is "home" or "security" or "notifications" or "assistant";
}
