using StackExchange.Redis;
using System;
using System.Linq;
using System.Timers;
using System.Windows;

namespace BitcoinPriceViewer
{
    public partial class MainWindow : Window
    {
        private static ConnectionMultiplexer _redis;
        private static IDatabase _db;
        private static System.Timers.Timer _timer;
        private DateTime _startTime;
        private double _startPrice;
        private double _minPrice;
        private double _maxPrice;

        public MainWindow()
        {

            InitializeComponent();
            _redis = ConnectionMultiplexer.Connect("localhost:6379,abortConnect=false,connectTimeout=10000");
            if (!_redis.IsConnected)
            {
                return;
            }
            _db = _redis.GetDatabase();
            _startTime = DateTime.Now;
            FetchInitialPrice();
            StartTimer();
        }



        private async void FetchInitialPrice()
        {
            var firstPrice = await GetFirstPrice();
            _startPrice = firstPrice;
            StartInfoText.Text = $"{_startTime} - {_startPrice}";
        }

        private void StartTimer()
        {
            _timer = new System.Timers.Timer(1000);
            _timer.Elapsed += UpdateUI;
            _timer.Start();
        }

        private async void UpdateUI(object sender, ElapsedEventArgs e)
        {
            Dispatcher.Invoke(async () =>
            {
                CurrentTimeText.Text = DateTime.Now.ToString("o");
                await UpdatePrices();
            });
        }

        private async Task UpdatePrices()
        {
            if (_redis.IsConnected)
            {
                var prices = await _db.ListRangeAsync("bitcoin_prices", 0, -1);
                var parsedPrices = prices.Select(p => ParsePrice(p)).ToList();

                if (parsedPrices.Count > 0)
                {
                    double min = parsedPrices.Min(p => p.Price);
                    double max = parsedPrices.Max(p => p.Price);

                    MinInfoText.Text = $"{parsedPrices.First(p => p.Price == min).Time} - {min}";
                    MaxInfoText.Text = $"{parsedPrices.First(p => p.Price == max).Time} - {max}";

                    RecentPricesList.ItemsSource = parsedPrices.Select(p => $"{p.Time} - {p.Price}").
                        Skip(Math.Max(0, parsedPrices.Count - 50));
                }
            }
        }

        private async Task<double> GetFirstPrice()
        {
            if (_redis.IsConnected)
            {
                var prices = await _db.ListRangeAsync("bitcoin_prices", 0, 0);
                return double.Parse(prices.First().ToString().Split("|")[1]);
            }
            return 0;
        }

        private (DateTime Time, double Price) ParsePrice(string entry)
        {
            var parts = entry.Split("|");
            return (DateTime.Parse(parts[0]), double.Parse(parts[1]));
        }
    }
}
