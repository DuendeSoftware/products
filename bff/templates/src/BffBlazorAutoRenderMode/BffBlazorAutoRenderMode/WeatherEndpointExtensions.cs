using Duende.Bff;

namespace BffBlazorAutoRenderMode;

public static class WeatherEndpointExtensions
{
    public static void MapWeatherEndpoints(this WebApplication app) => app.MapGet("/WeatherForecast", async (IWeatherClient weatherClient) => await weatherClient.GetWeatherForecasts())
            .RequireAuthorization()
            .AsBffApiEndpoint();


}
