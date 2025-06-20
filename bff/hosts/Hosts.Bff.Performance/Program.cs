// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Hosts.Bff.Performance.Services;

var builder = Host.CreateApplicationBuilder();

builder.Services.Configure<ApiSettings>(builder.Configuration);
// builder.Services.Configure<BffSettings>(builder.Configuration);
// builder.Services.Configure<IdentityServerSettings>(builder.Configuration);

builder.Services.AddHostedService<ApiHostedService>();
// builder.Services.AddHostedService<IdentityServerService>();
// builder.Services.AddHostedService<SingleFrontendBffService>();
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
