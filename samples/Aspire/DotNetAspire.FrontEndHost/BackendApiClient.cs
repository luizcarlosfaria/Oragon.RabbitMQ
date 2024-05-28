namespace DotNetAspire.FrontEndHost;

public class BackendApiClient(HttpClient httpClient)
{
    public async Task<WeatherForecast[]> GetWeatherAsync()
    {
        return await httpClient.GetFromJsonAsync<WeatherForecast[]>("/weatherforecast") ?? [];
    }

    public async Task EnqueueAsync(string itemId)
    {
        await httpClient.PostAsJsonAsync("/enqueue", new MsgToEnqueue(itemId));
    }
}

public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(this.TemperatureC / 0.5556);
}
record MsgToEnqueue(string ItemId)
{
}
