using DotNetAspireApp.Common.Messages.Commands;

namespace DotNetAspireApp.Web;

public class BackendApiClient(HttpClient httpClient)
{
    public async Task<WeatherForecast[]> GetWeatherAsync(int maxItems = 10, CancellationToken cancellationToken = default)
    {
        List<WeatherForecast>? forecasts = null;

        await foreach (var forecast in httpClient.GetFromJsonAsAsyncEnumerable<WeatherForecast>("/weatherforecast", cancellationToken).ConfigureAwait(false))
        {
            if (forecasts?.Count >= maxItems)
            {
                break;
            }
            if (forecast is not null)
            {
                forecasts ??= [];
                forecasts.Add(forecast);
            }
        }

        return forecasts?.ToArray() ?? [];
    }

    public async Task EnqueueAsync(DoSomethingRequest request)
    {
        _ = await httpClient.PostAsJsonAsync("/enqueue", request).ConfigureAwait(false);
    }
}

public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(this.TemperatureC / 0.5556);
}

