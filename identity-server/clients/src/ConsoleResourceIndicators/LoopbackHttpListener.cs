// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConsoleResourceIndicators;

public class LoopbackHttpListener : IDisposable
{
    private const int DefaultTimeout = 60 * 5; // 5 mins (in seconds)

    private WebApplication _host;
    private TaskCompletionSource<string> _source = new TaskCompletionSource<string>();
    private string _url;

    public string Url => _url;

    public LoopbackHttpListener(int port, string path = null)
    {
        path ??= string.Empty;
        if (path.StartsWith('/'))
        {
            path = path.Substring(1);
        }

        _url = $"http://127.0.0.1:{port}/{path}";

        var builder = WebApplication.CreateBuilder();
        _ = builder.Logging.ClearProviders();
        _host = builder.Build();
        _host.Urls.Add(_url);
        Configure(_host);
        _host.Start();
    }

    public void Dispose() => _ = Task.Run(async () =>
    {
        await Task.Delay(500);
        await _host.DisposeAsync();
    });

    private void Configure(IApplicationBuilder app) => app.Run(async ctx =>
                                                            {
                                                                if (ctx.Request.Method == "GET")
                                                                {
                                                                    await SetResultAsync(ctx.Request.QueryString.Value, ctx);
                                                                    return;
                                                                }

                                                                ctx.Response.StatusCode = 405;
                                                            });

    private async Task SetResultAsync(string value, HttpContext ctx)
    {
        _ = _source.TrySetResult(value);

        try
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/html";
            await ctx.Response.WriteAsync("<h1>You can now return to the application.</h1>");
            await ctx.Response.Body.FlushAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            ctx.Response.StatusCode = 400;
            ctx.Response.ContentType = "text/html";
            await ctx.Response.WriteAsync("<h1>Invalid request.</h1>");
            await ctx.Response.Body.FlushAsync();
        }
    }

    public Task<string> WaitForCallbackAsync(int timeoutInSeconds = DefaultTimeout)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(timeoutInSeconds * 1000);
            _ = _source.TrySetCanceled();
        });

        return _source.Task;
    }
}
