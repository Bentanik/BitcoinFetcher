using StackExchange.Redis;
using Microsoft.Extensions.Configuration;

namespace BitcoinFetcherConsole;

class Program
{
    private static IConfigurationRoot configuration;
    private static string redisConnectionString;
    private static string bitcoinApiUrl;
    private static TimeSpan fetchInterval;

    static async Task Main(string[] args)
    {
        LoadConfiguration();

        using (var redis = ConnectionMultiplexer.Connect(redisConnectionString))
        {
            var db = redis.GetDatabase();

            while (true)
            {
                try
                {
                    DateTime utcNow = DateTime.UtcNow;
                    TimeZoneInfo vietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                    DateTime vietnamDateTime = TimeZoneInfo.ConvertTimeFromUtc(utcNow, vietnamTimeZone);
                    var price = await FetchBitcoinPriceAsync();

                    var message = $"{vietnamDateTime}|{price}";
                    await db.ListRightPushAsync("bitcoin_prices", message);

                    Console.WriteLine($"[{vietnamDateTime}] Price Bitcoin: {price} USD");
                    await Task.Delay(fetchInterval);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
        }
    }

    private static void LoadConfiguration()
    {
        configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var _redisHost = configuration["Redis:Host"];
        var _redisPort = int.Parse(configuration["Redis:Port"]);
        redisConnectionString = $"{_redisHost}:{_redisPort}";
        bitcoinApiUrl = configuration["BitcoinApi:Url"];
        fetchInterval = TimeSpan.FromSeconds(int.Parse(configuration["AppSettings:FetchIntervalInSeconds"]));
    }

    private static async Task<decimal> FetchBitcoinPriceAsync()
    {
        using (HttpClient client = new HttpClient())
        {
            var response = await client.GetStringAsync(bitcoinApiUrl);
            dynamic json = Newtonsoft.Json.JsonConvert.DeserializeObject(response);

            return json.bpi.USD.rate_float;
        }
    }
}