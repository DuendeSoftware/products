using System.Net.Http.Json;
using System.Text.Json;

/// <summary>
/// this is an example of a class that would access the data via a web service. This is typically
/// what you'd do in webassembly. 
/// Note that it implements the same interface as the <see cref="ServerWeatherClient"/>
/// when it's rendering on the server. 
/// </summary>
/// <param name="client"></param>
internal class WeatherClient(HttpClient client) : IWeatherClient
{
    public async Task<WeatherForecast[]> GetWeatherForecasts() => await client.GetFromJsonAsync<WeatherForecast[]>("WeatherForecast")
                                                                  ?? throw new JsonException("Failed to deserialize");
}

/// <summary>
/// Abstraction of the class that retrieves weather data on the client or the server
/// </summary>
public interface IWeatherClient
{
    Task<WeatherForecast[]> GetWeatherForecasts();
}
