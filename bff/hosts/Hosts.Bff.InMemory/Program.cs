// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Bff;

Console.Title = "BFF";

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var serviceProviderAccessor = new ServiceProviderAccessor();

var app = builder
    .ConfigureServices(() => serviceProviderAccessor.ServiceProvider ?? throw new InvalidOperationException("Service Provider must be set"))
    .ConfigurePipeline();

serviceProviderAccessor.ServiceProvider = app.Services;

app.Run();
