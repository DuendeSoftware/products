namespace IdentityServerHost.Pages;

internal static partial class Log
{
    [LoggerMessage(EventId = EventIds.InvalidId, Level = LogLevel.Error, Message = "Invalid id {Id}")]
    public static partial void InvalidId(this ILogger logger, string? id);

    [LoggerMessage(EventId = EventIds.InvalidBackchannelLoginId, Level = LogLevel.Warning, Message = "Invalid backchannel login id {Id}")]
    public static partial void InvalidBackchannelLoginId(this ILogger logger, string? id);

    [LoggerMessage(EventId = EventIds.ExternalClaims, Level = LogLevel.Debug, Message = "External claims: {Claims}")]
    public static partial void ExternalClaims(this ILogger logger, IEnumerable<string> claims);

    [LoggerMessage(EventId = EventIds.NoMatchingBackchannelLoginRequest, Level = LogLevel.Error, Message = "No backchannel login request matching id: {Id}")]
    public static partial void NoMatchingBackchannelLoginRequest(this ILogger logger, string id);

    [LoggerMessage(EventId = EventIds.NoConsentMatchingRequest, Level = LogLevel.Error, Message = "No consent request matching request: {ReturnUrl}")]
    public static partial void NoConsentMatchingRequest(this ILogger logger, string returnUrl);
}

internal static class EventIds
{
    private const int UIEventsStart = 10000;

    //////////////////////////////
    // Consent
    //////////////////////////////
    private const int ConsentEventsStart = UIEventsStart + 1000;
    public const int InvalidId = ConsentEventsStart + 0;
    public const int NoConsentMatchingRequest = ConsentEventsStart + 1;

    //////////////////////////////
    // External Login
    //////////////////////////////
    private const int ExternalLoginEventsStart = UIEventsStart + 2000;
    public const int ExternalClaims = ExternalLoginEventsStart + 0;

    //////////////////////////////
    // CIBA
    //////////////////////////////
    private const int CibaEventsStart = UIEventsStart + 3000;
    public const int InvalidBackchannelLoginId = CibaEventsStart + 0;
    public const int NoMatchingBackchannelLoginRequest = CibaEventsStart + 1;



}
