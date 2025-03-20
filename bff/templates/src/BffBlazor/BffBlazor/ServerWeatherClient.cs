namespace BffBlazor;


/// <summary>
/// This is an example of a server-side class that accesses data from a datasource (IE: a database)
/// and makes it available to the application. On the server, the component can directly access the database,
/// whereas on the client, the component needs to go via a HTTP Client. See the <see cref="WeatherClient"/>
/// </summary>
internal class ServerWeatherClient() : IWeatherClient
{
    public Task<WeatherForecast[]> GetWeatherForecasts()
    {
        var startDate = DateOnly.FromDateTime(DateTime.Now);
        return Task.FromResult(Enumerable.Range(1, 5).Select(index => new WeatherForecast
        {
            Date = startDate.AddDays(index),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        }).ToArray());
    }

    private static readonly string[] Summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];
}
