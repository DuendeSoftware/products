// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Hosts.Bff.Performance.Services;

var configurationBuilder = new ConfigurationBuilder()
    .AddCommandLine(args)
    .AddEnvironmentVariables();
var configuration = configurationBuilder.Build();
var startupConfiguration = new StartupConfiguration();
configuration.Bind(startupConfiguration);

Console.WriteLine($"Enabled Services: {startupConfiguration.Services}");

var builder = Host.CreateApplicationBuilder();

if (startupConfiguration.IsServiceEnabled("api"))
{
    builder.Services.Configure<ApiSettings>(builder.Configuration);
    builder.Services.AddHostedService<ApiHostedService>();
}

if (startupConfiguration.IsServiceEnabled("idsrv"))
{
    builder.Services.Configure<IdentityServerSettings>(builder.Configuration);
    builder.Services.AddHostedService<IdentityServerService>();
}

if (startupConfiguration.IsServiceEnabled("bff"))
{
    builder.Services.Configure<BffSettings>(builder.Configuration);
    builder.Services.AddHostedService<SingleFrontendBffService>();
}
// builder.Services.AddHostedService<MultiFrontendBffService>();
// Add services to the container.

var app = builder.Build();

// Configure the HTTP request pipeline.

// spin up multiple applications:
// Plain yarp


// single frontend
// multi-frontend
// bff with server side EF sessions

app.Run();

public class StartupConfiguration
{
    public string Services { get; set; } = string.Empty;

    public bool IsServiceEnabled(string serviceName) =>
        Services.Contains(serviceName) || Services.Equals("all");
}
