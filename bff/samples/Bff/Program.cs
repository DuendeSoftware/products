using System;
using Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

Console.Title = "BFF";

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var serviceProviderAccessor = new ServiceProviderAccessor();

var app = builder
    .ConfigureServices(() => serviceProviderAccessor.ServiceProvider ?? throw new InvalidOperationException("Service Provider must be set"))
    .ConfigurePipeline();

serviceProviderAccessor.ServiceProvider = app.Services;

app.Run();