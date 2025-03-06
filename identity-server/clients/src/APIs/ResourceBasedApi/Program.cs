// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace ResourceBasedApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.Title = "Resource Based API";

            BuildWebHost(args).Run();
        }

        public static IHost BuildWebHost(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("ResourceBasedApi", LogEventLevel.Debug)
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}", theme: AnsiConsoleTheme.Code)
                .CreateLogger();

            return Host.CreateDefaultBuilder(args)
                    .ConfigureWebHostDefaults(webBuilder =>
                    {
                        webBuilder.UseStartup<Startup>();
                    })
                    .UseSerilog()
                    .Build();
        }
    }
}