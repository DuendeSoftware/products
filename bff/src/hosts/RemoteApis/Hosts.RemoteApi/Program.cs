using Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder
    .ConfigureServices()
    .ConfigurePipeline();

app.Run();
