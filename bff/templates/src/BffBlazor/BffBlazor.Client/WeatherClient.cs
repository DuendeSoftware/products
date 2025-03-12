using System.Net.Http.Json;
using System.Text.Json;

internal class WeatherClient(HttpClient client) : IWeatherClient
{
    public async Task<WeatherForecast[]> GetWeatherForecasts() => await client.GetFromJsonAsync<WeatherForecast[]>("WeatherForecast")
                                                                  ?? throw new JsonException("Failed to deserialize");
}

public interface IWeatherClient
{
    Task<WeatherForecast[]> GetWeatherForecasts();
}
