// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

using Serilog;
using Serilog.Core;
using Serilog.Extensions.Logging;

namespace Duende.Xunit.Playwright;

// TODO - Do we need this to be IAsyncLifetime?
// TODO - Rename?
public class AppHostFixture(IAppHostServiceRoutes routes) : IAsyncLifetime
{
    private WriteTestOutput? _activeWriter;
    private Logger _logger = null!;


    public Task InitializeAsync()
    {
        var loggerConfiguration = new LoggerConfiguration()
            .WriteTo
            .TextWriter(new DelegateTextWriter(WriteLogs),
                outputTemplate: "{Message} - {SourceContext} {NewLine}");

        _logger = loggerConfiguration.CreateLogger();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public IDisposable ConnectLogger(WriteTestOutput output)
    {
        _activeWriter = output;
        return new DelegateDisposable(() => _activeWriter = null);
    }

    private void WriteLogs(string logMessage) => _activeWriter?.Invoke(logMessage);

    /// <summary>
    /// This method builds an http client.
    /// </summary>
    /// <param name="clientName"></param>
    /// <returns></returns>
    public HttpClient CreateHttpClient(string clientName)
    {
        var baseAddress = GetUrlTo(clientName);
        var inner = new SocketsHttpHandler
        {
            // We need to disable cookies and follow redirects
            // because we do this manually (see below).
            UseCookies = false,
            AllowAutoRedirect = false
        };

        // Log every call that's made (including if it was part of a redirect).
        var loggingHandler = new RequestLoggingHandler(CreateLogger<RequestLoggingHandler>(), _ => true)
        {
            InnerHandler = inner
        };

        // Manually take care of cookies (see reason why above)
        var cookieHandler = new CookieHandler(loggingHandler, new CookieContainer());

        // Follow redirects when needed.
        var redirectHandler = new AutoFollowRedirectHandler(CreateLogger<AutoFollowRedirectHandler>())
        {
            InnerHandler = cookieHandler
        };

        // Return an http client that follows redirects, uses cookies and logs all requests.
        // For aspire, this is needed otherwise cookies are shared, but it also
        // gives a much clearer debug output (each request gets logged).
        return new HttpClient(redirectHandler)
        {
            BaseAddress = baseAddress
        };
    }

    public Uri GetUrlTo(string clientName) => routes.UrlTo(clientName);

    private ILogger<T> CreateLogger<T>()
    {
        var loggerProvider = new SerilogLoggerProvider(_logger);
        return new LoggerFactory([loggerProvider]).CreateLogger<T>();
    }
}
