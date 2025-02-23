using System;
using Api.Isolated;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

Console.Title = "Isolated Api";

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var app = builder
    .ConfigureServices()
    .ConfigurePipeline();

app.Run();
