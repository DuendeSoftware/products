// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Logging;

internal class SanitizedLogger<T>
{
    private readonly ILogger _logger;

    public SanitizedLogger(ILogger logger) => _logger = logger;

    public void LogTrace(string message, params object[] args)
    {
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace(message, args.Select(ILoggerDevExtensions.SanitizeLogParameter).ToArray());
        }
    }

    public void LogDebug(string message, params object[] args)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
#pragma warning disable CA2254 // Both the message template and any properties for the template are parameters here
            LoggerExtensions.LogDebug(_logger, message, args.Select(ILoggerDevExtensions.SanitizeLogParameter).ToArray());
#pragma warning restore CA2254
        }
    }

    public void LogInformation(string message, params object[] args)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
#pragma warning disable CA2254 // Both the message template and any properties for the template are parameters here
            _logger.LogInformation(message, args.Select(ILoggerDevExtensions.SanitizeLogParameter).ToArray());
#pragma warning restore CA2254
        }
    }

    public void LogWarning(string message, params object[] args)
    {
        if (_logger.IsEnabled(LogLevel.Warning))
        {
#pragma warning disable CA2254 // Both the message template and any properties for the template are parameters here
            _logger.LogWarning(message, args.Select(ILoggerDevExtensions.SanitizeLogParameter).ToArray());
#pragma warning restore CA2254
        }
    }

    public void LogError(string message, params object[] args)
    {
        if (_logger.IsEnabled(LogLevel.Error))
        {
#pragma warning disable CA2254 // Both the message template and any properties for the template are parameters here
            _logger.LogError(message, args.Select(ILoggerDevExtensions.SanitizeLogParameter).ToArray());
#pragma warning restore CA2254
        }
    }

    public void LogCritical(Exception exception, string message, params object[] args)
    {
        if (_logger.IsEnabled(LogLevel.Critical))
        {
#pragma warning disable CA2254 // Both the message template and any properties for the template are parameters here
            _logger.LogCritical(exception, message, args.Select(ILoggerDevExtensions.SanitizeLogParameter).ToArray());
#pragma warning restore CA2254
        }
    }

    public ILogger ToILogger() => _logger;
}
