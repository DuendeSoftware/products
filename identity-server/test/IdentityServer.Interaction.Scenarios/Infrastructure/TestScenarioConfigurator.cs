// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;
using Duende.IdentityServer.UI.Infra;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Interaction.Tests.Infrastructure;

/// <summary>
/// A minimal <see cref="IScenarioConfigurator"/> for tests — no OTel, just console logging.
/// </summary>
internal sealed class TestScenarioConfigurator(ITestOutputHelper output) : IScenarioConfigurator
{
    public WebApplicationBuilder CreateBuilder(string serviceName)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.Logging.ClearProviders();
        builder.Logging.AddXUnit(output);
        builder.Logging.SetMinimumLevel(LogLevel.Error);
        builder.WebHost.UseUrls("https://127.0.0.1:0");
        builder.Services.AddSingleton<IStartupFilter, ExceptionLoggingFilter>();
        return builder;
    }
}

public class ExceptionLoggingFilter : IStartupFilter
{
    private static readonly Action<ILogger, Exception?> _logUnhandledException =
        LoggerMessage.Define(LogLevel.Error, new EventId(0, "UnhandledException"), "Unhandled exception");

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        (builder) =>
        {
            builder.Use(async (c, n) =>
            {
                try
                {
                    await n();
                }
                catch (Exception e)
                {
                    var logger = c.RequestServices.GetRequiredService<ILogger<TestScenarioConfigurator>>();
                    _logUnhandledException(logger, e);
                    throw;
                }
            });

            next(builder);
        };
}

/// <summary>
/// An <see cref="ITestOutputHelper"/> proxy that buffers messages when no test is attached,
/// then flushes buffered startup logs when a test detaches.
/// </summary>
public class ProxyOutput : ITestOutputHelper
{
    private readonly object _lock = new();
    private readonly StringBuilder _buffer = new();
    private ITestOutputHelper? _inner;
    private bool _startupFlushed;

    /// <summary>
    /// Attach the current test's output helper. Messages will flow directly to it.
    /// </summary>
    public void Attach(ITestOutputHelper output)
    {
        lock (_lock)
        {
            _inner = output;
        }
    }

    /// <summary>
    /// Detach the current test's output helper. Flushes any buffered startup logs
    /// to the output before disconnecting.
    /// </summary>
    public void Detach()
    {
        lock (_lock)
        {
            if (_inner is not null && !_startupFlushed && _buffer.Length > 0)
            {
                _inner.WriteLine("");
                _inner.WriteLine("════════════════════════════════════════");
                _inner.WriteLine("  STARTUP LOGS (buffered before test)");
                _inner.WriteLine("════════════════════════════════════════");
                _inner.WriteLine(_buffer.ToString());
                _inner.WriteLine("════════════════════════════════════════");
                _startupFlushed = true;
            }

            _inner = null;
        }
    }

    public void Write(string message)
    {
        lock (_lock)
        {
            if (_inner is not null)
            {
                _inner.Write(message);
            }
            else
            {
                _buffer.Append(message);
            }
        }
    }

    public void Write(string format, params object[] args)
    {
        lock (_lock)
        {
            if (_inner is not null)
            {
                _inner.Write(format, args);
            }
            else
            {
                _buffer.AppendFormat(format, args);
            }
        }
    }

    public void WriteLine(string message)
    {
        lock (_lock)
        {
            if (_inner is not null)
            {
                _inner.WriteLine(message);
            }
            else
            {
                _buffer.AppendLine(message);
            }
        }
    }

    public void WriteLine(string format, params object[] args)
    {
        lock (_lock)
        {
            if (_inner is not null)
            {
                _inner.WriteLine(format, args);
            }
            else
            {
                _buffer.AppendFormat(format, args);
                _buffer.AppendLine();
            }
        }
    }

    public string Output => _inner?.Output ?? "";
}
