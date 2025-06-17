// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Hosts.Bff.Performance.Services;

var builder = Host.CreateApplicationBuilder();

builder.AddServiceDefaults();

builder.Services.AddHostedService<SingleFrontendBffService>();
builder.Services.AddHostedService<MultiFrontendBffService>();
// Add services to the container.

var app = builder.Build();

// Configure the HTTP request pipeline.

// spin up multiple applications:
// Plain yarp


// single frontend
// multi-frontend
// bff with server side EF sessions




app.Run();
