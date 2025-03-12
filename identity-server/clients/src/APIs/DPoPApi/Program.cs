// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Serilog;
using Serilog.Events;

namespace DPoPApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.Title = "DPoP API";

            BuildWebHost(args).Run();
        }

        public static IHost BuildWebHost(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("DPoPApi", LogEventLevel.Debug)
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}")
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
