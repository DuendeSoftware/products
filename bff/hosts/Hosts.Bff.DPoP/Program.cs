// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Bff.DPoP;

Console.Title = "BFF.DPoP";

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var app = builder
    .ConfigureServices()
    .ConfigurePipeline();

app.Run();
