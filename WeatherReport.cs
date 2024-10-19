using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json; 
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Company.Function
{
    public class WeatherReport
    {
        private readonly ILogger _logger;
        private static readonly HttpClient httpClient = new HttpClient();

        private const string OpenWeatherMapApiKey = ""; //  API key 
        private const string OpenWeatherMapUrl = "http://api.openweathermap.org/data/2.5/weather?q=Si Racha&appid="; // Set-City
        private const string LineNotifyToken = ""; //  LINE Notify token 
        private const string LineNotifyUrl = "https://notify-api.line.me/api/notify";

        public WeatherReport(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<WeatherReport>();
        }

        [Function("WeatherReport")]
        public async Task Run([TimerTrigger("*/30 * * * * *")] TimerInfo myTimer) // Trigger at 30 second
        {
            _logger.LogInformation($"Timer Trigger at: {DateTime.Now}");

            var weatherData = await GetWeatherData();
            if (weatherData != null)
            {
                await SendLineNotification(weatherData);
            }
            else
            {
                _logger.LogWarning("Cannot get data");
            }
        }

        private async Task<string> GetWeatherData()
        {
            try
            {
                var response = await httpClient.GetStringAsync($"{OpenWeatherMapUrl}{OpenWeatherMapApiKey}");
                _logger.LogInformation("Weather report successfully.");
                return response;
            }
            catch (HttpRequestException httpRequestException)
            {
                _logger.LogError($"HTTP Error: {httpRequestException.Message}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                return null;
            }
        }

        private async Task SendLineNotification(string weatherData)
        {
            // Convert JSON to object
            using JsonDocument doc = JsonDocument.Parse(weatherData);
            var weatherInfo = doc.RootElement;

            string country = weatherInfo.GetProperty("sys").GetProperty("country").GetString();
            string city = weatherInfo.GetProperty("name").GetString();
            string description = weatherInfo.GetProperty("weather")[0].GetProperty("description").GetString();
            int clouds = weatherInfo.GetProperty("clouds").GetProperty("all").GetInt32();
            double temperature = weatherInfo.GetProperty("main").GetProperty("temp").GetDouble() - 273.15; //  Kelvin to Celsius
            double feelsLike = weatherInfo.GetProperty("main").GetProperty("feels_like").GetDouble() - 273.15; //  Kelvin to Celsius
            int humidity = weatherInfo.GetProperty("main").GetProperty("humidity").GetInt32();

            var message = $"Weather Report:\n" +
                          $"Country: {country}\n" +
                          $"City: {city}\n" +
                          $"Weather: {description}\n" +
                          $"Cloud: {clouds}%\n" +
                          $"Temperature: {temperature:F1} °C\n" + 
                          $"Feel Like: {feelsLike:F1} °C\n" +
                          $"Humidity: {humidity}%\n" +
                          $"Reported by Chayathon.R";

            var content = new StringContent($"message={Uri.EscapeDataString(message)}", Encoding.UTF8, "application/x-www-form-urlencoded");

            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", LineNotifyToken);

            try
            {
                var response = await httpClient.PostAsync(LineNotifyUrl, content);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Send to LINE success.");
                }
                else
                {
                    _logger.LogError($"Cant to send to LINE: {response.StatusCode}");
                }
            }
            catch (HttpRequestException httpRequestException)
            {
                _logger.LogError($"HTTP Error when sending to LINE: {httpRequestException.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending to LINE: {ex.Message}");
            }
        }
    }
}
