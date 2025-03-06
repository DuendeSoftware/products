// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace BffBlazor;

public static class WeatherEndpointExtensions
{
    public static void MapWeatherEndpoints(this WebApplication app)
    {
        app.MapGet("/WeatherForecast", () =>
        {
            var startDate = DateOnly.FromDateTime(DateTime.Now);
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = startDate.AddDays(index),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            }).ToArray();
        }).RequireAuthorization().AsBffApiEndpoint();
    }

    private static readonly string[] Summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];
}