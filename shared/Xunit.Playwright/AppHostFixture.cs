// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Aspire.Hosting;
using Microsoft.Extensions.Logging;

#if !DEBUG_NCRUNCH
using Microsoft.Extensions.Logging.Console;
#endif

using Serilog;
using Serilog.Core;
using Serilog.Extensions.Logging;

namespace Duende.Xunit.Playwright;

public class AppHostFixture<THost>(IAppHostServiceRoutes routes) : IAsyncLifetime where THost : class
{
    private readonly TextWriter _startupLogs = new StringWriter();
    private WriteTestOutput? _activeWriter;
    private DistributedApplication? _app;
    private Logger _logger = null!;

    public bool UsingAlreadyRunningInstance { get; private set; }
    public string StartupLogs => _startupLogs.ToString() ?? string.Empty;

    public async Task InitializeAsync()
    {
        using var startupLogWriter = ConnectLogger(s => _startupLogs.Write(s));


        var loggerConfiguration = new LoggerConfiguration()
            .WriteTo
            .TextWriter(new DelegateTextWriter(WriteLogs),
                outputTemplate: "{Message} - {SourceContext} {NewLine}");

        _logger = loggerConfiguration.CreateLogger();

        UsingAlreadyRunningInstance = await CheckIfAspireIsRunning();

        if (UsingAlreadyRunningInstance)
        {
            WriteLogs("Running tests against real test server");
            return;
        }

#if !DEBUG_NCRUNCH

        // The console logger is cluttering up the test output
        void RemoveConsoleLogger(ILoggingBuilder x)
        {
            var collection = x.Services;
            for (var i = collection.Count - 1; i >= 0; i--)
            {
                var descriptor = collection[i];
                if (descriptor.ServiceType == typeof(ILoggerProvider) && descriptor.ImplementationType == typeof(ConsoleLoggerProvider))
                {
                    collection.RemoveAt(i);
                }
            }
        }

        // Not running in ncrunch AND no service found running.
        // So, create an AppHost that will be used for the duration of this test run.
        var appHost = await DistributedApplicationTestingBuilder
                .CreateAsync<THost>();
        appHost.Configuration["DcpPublisher:RandomizePorts"] = "false";

        appHost.Services.ConfigureHttpClientDefaults(c => c.ConfigurePrimaryHttpMessageHandler(() =>
            new SocketsHttpHandler
            {
                UseCookies = false,
                AllowAutoRedirect = false
            }));

        appHost.Services.AddLogging(x =>
        {
            RemoveConsoleLogger(x);
            x.AddSerilog(_logger);
        });

        _app = await appHost.BuildAsync();

        var resourceNotificationService = _app.Services
            .GetRequiredService<ResourceNotificationService>();

        await _app.StartAsync();

        // Wait for all the services so that their logs are mostly written.

        foreach (var resource in routes.ServiceNames)
        {
            await resourceNotificationService.WaitForResourceAsync(
                    resource,
                    KnownResourceStates.Running
                )
                .WaitAsync(TimeSpan.FromSeconds(30));
        }

#else
        _app = null!;
#endif //#DEBUG_NCRUNCH
    }


    public async Task DisposeAsync()
    {
        if (_app != null)
        {
            await _app.DisposeAsync();
        }
    }

    public IDisposable ConnectLogger(WriteTestOutput output)
    {
        _activeWriter = output;
        return new DelegateDisposable(() => _activeWriter = null);
    }

    private async Task<bool> CheckIfAspireIsRunning()
    {
        try
        {
            var client = new HttpClient();
            client.Timeout = TimeSpan.FromMilliseconds(1000);

            // TODO - This is specific to BFF
            var response = await client.GetAsync("https://localhost:17052");

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            return false;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    private void WriteLogs(string logMessage) => _activeWriter?.Invoke(logMessage);

    /// <summary>
    /// This method builds an http client.
    /// </summary>
    /// <param name="clientName"></param>
    /// <returns></returns>
    public HttpClient CreateHttpClient(string clientName)
    {
        HttpMessageHandler inner;
        Uri? baseAddress;

        if (UsingAlreadyRunningInstance)
        {
            var url = GetUrlTo(clientName);
            baseAddress = url;

            inner = new SocketsHttpHandler
            {
                // We need to disable cookies and follow redirects
                // because we do this manually (see below).
                UseCookies = false,
                AllowAutoRedirect = false
            };
        }
        else
        {
#if DEBUG_NCRUNCH
        // This should not be reached for NCrunch because either the service is already running
        // or the test base has thrown a SkipException.
        throw new InvalidOperationException("This should not be reached in NCrunch");
#else
            // If we're here, that means that we need to create an http client that's pointing to
            // aspire.
            if (_app == null)
            {
                throw new NotSupportedException("App should not be null");
            }

            var client = _app.CreateHttpClient(clientName);
            baseAddress = client.BaseAddress;

            // We can't directly use the HTTP Client, because we need cookie support, but if we
            // enable that the cookies get shared across multiple requests
            // https://github.com/dotnet/AspNetCore.Docs/issues/15848
            // By wrapping the http client, then handling all the cookies
            // ourselves, we bypass this problem.
            inner = new CloningHttpMessageHandler(client);
#endif
        }

        // Log every call that's made (including if it was part of a redirect).
        var loggingHandler =
            new RequestLoggingHandler(
                CreateLogger<RequestLoggingHandler>()
                , _ => true)
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

    public Uri GetUrlTo(string clientName)
    {
        if (UsingAlreadyRunningInstance)
        {
            return routes.UrlTo(clientName);
        }
        else
        {
#if !DEBUG_NCRUNCH
            if (_app == null)
            {
                throw new NullReferenceException("App should not be null");
            }

            return _app.GetEndpoint(clientName);
#else
            Skip.If(true, "When running the Host.Tests using NCrunch, you must start the Hosts.AppHost project manually. IE: dotnet run -p bff/samples/Hosts.AppHost. Or start without debugging from the UI. ");
            return null!;
#endif
        }
    }

    private ILogger<T> CreateLogger<T>()
    {
        var loggerProvider = new SerilogLoggerProvider(_logger);
        return new LoggerFactory([loggerProvider]).CreateLogger<T>();
    }
}
