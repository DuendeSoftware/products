// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using IdentityServer;

Console.Title = "IdentityServer";
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var app = builder
    .ConfigureServices()
    .ConfigurePipeline();

app.Run();
