// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Logging;

internal class SanitizedLogger<T>
{
    private readonly ILogger<T> _logger;

    public SanitizedLogger(ILogger<T> logger) => _logger = logger;

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
            LoggerExtensions.LogDebug(_logger, message, args.Select(ILoggerDevExtensions.SanitizeLogParameter).ToArray());
        }
    }

    public void LogInformation(string message, params object[] args)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(message, args.Select(ILoggerDevExtensions.SanitizeLogParameter).ToArray());
        }
    }

    public void LogWarning(string message, params object[] args)
    {
        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning(message, args.Select(ILoggerDevExtensions.SanitizeLogParameter).ToArray());
        }
    }

    public void LogError(string message, params object[] args)
    {
        if (_logger.IsEnabled(LogLevel.Error))
        {
            _logger.LogError(message, args.Select(ILoggerDevExtensions.SanitizeLogParameter).ToArray());
        }
    }

    public void LogCritical(Exception exception, string message, params object[] args)
    {
        if (_logger.IsEnabled(LogLevel.Critical))
        {
            _logger.LogCritical(exception, message, args.Select(ILoggerDevExtensions.SanitizeLogParameter).ToArray());
        }
    }

    public ILogger ToILogger() => _logger;
}
