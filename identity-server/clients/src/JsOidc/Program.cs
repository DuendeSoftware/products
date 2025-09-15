// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace JsOidc;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var app = builder.Build();

        app.UseDefaultFiles();

        // enable to test w/ CSP
        //app.Use(async (ctx, next) =>
        //{
        //    ctx.Response.OnStarting(() =>
        //    {
        //        if (ctx.Response.ContentType?.StartsWith("text/html") == true)
        //        {
        //            ctx.Response.Headers.Add("Content-Security-Policy", "default-src 'self'; connect-src http://localhost:5000 http://localhost:3721; frame-src 'self' http://localhost:5000");
        //        }
        //        return Task.CompletedTask;
        //    });

        //    await next();
        //});

        app.UseStaticFiles();

        app.Run();
    }
}
