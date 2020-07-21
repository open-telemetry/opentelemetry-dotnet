using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Threading.Tasks;

using OpenTelemetry.Samples.AspNet.Models;

namespace OpenTelemetry.Samples.AspNet.Controllers
{
    public class WeatherForecastController : ApiController
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        [HttpGet] // For testing traditional routing. Ex: https://localhost:XXXX/api/weatherforecast
        public async Task<IEnumerable<WeatherForecast>> Get()
        {
            // Build some dependency spans.

            await RequestGoogleHomPageViaHttpClient().ConfigureAwait(false);

            await this.RequestInvalidViaHttpClient().ConfigureAwait(false);

            await this.RequestValidThatReturnsFailedViaHttpClient().ConfigureAwait(false);

            await this.RequestValidThatSpawnsSubSpansViaHttpClient().ConfigureAwait(false);

            return GetWeatherForecast();
        }

        [Route("subroute/{customerId}")] // For testing attribute routing. Ex: https://localhost:XXXX/subroute/10
        [HttpGet]
        public async Task<IEnumerable<WeatherForecast>> Get(int customerId)
        {
            if (customerId < 0)
            {
                throw new ArgumentException();
            }

            // Making http calls here to serve as an example of
            // how dependency calls will be captured and treated
            // automatically as child of incoming request.

            RequestGoogleHomPageViaHttpWebRequestLegacySync();

            await RequestGoogleHomPageViaHttpWebRequestLegacyAsync().ConfigureAwait(false);

            RequestGoogleHomPageViaHttpWebRequestLegacyAsyncResult();

            return GetWeatherForecast();
        }

        private static IEnumerable<WeatherForecast> GetWeatherForecast()
        {
            var rng = new Random();
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = rng.Next(-20, 55),
                Summary = Summaries[rng.Next(Summaries.Length)]
            })
            .ToArray();
        }

        // Test successful dependency collection via HttpClient.
        private static async Task RequestGoogleHomPageViaHttpClient()
        {
            using var request = new HttpClient();

            using var response = await request.GetAsync("http://www.google.com").ConfigureAwait(false);

            response.EnsureSuccessStatusCode();
        }

        // Test exception dependency collection via HttpClient.
        private async Task RequestInvalidViaHttpClient()
        {
            try
            {
                using var request = new HttpClient();

                // This request is not available over SSL and will throw a handshake exception.

                using var response = await request.GetAsync(this.Url.Content("~/subroute/10").Replace("http", "https")).ConfigureAwait(false);

                Debug.Fail("Unreachable");
            }
            catch
            {
            }
        }

        // Test exception dependency collection via HttpClient.
        private async Task RequestValidThatReturnsFailedViaHttpClient()
        {
            using var request = new HttpClient();

            // This request will return a 500 error because customerId should be >= 0;

            using var response = await request.GetAsync(this.Url.Content("~/subroute/-1")).ConfigureAwait(false);

            Debug.Assert(response.StatusCode == HttpStatusCode.InternalServerError);
        }

        // Test successful dependency collection via HttpClient.
        private async Task RequestValidThatSpawnsSubSpansViaHttpClient()
        {
            using var request = new HttpClient();

            // This request will return successfully and cause a bunch of sub-spans;

            using var response = await request.GetAsync(this.Url.Content("~/subroute/10")).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();
        }

        // Test dependency collection via legacy HttpWebRequest sync.
        private static void RequestGoogleHomPageViaHttpWebRequestLegacySync()
        {
            var request = WebRequest.Create("http://www.google.com/?sync");

            using var response = request.GetResponse();
        }

        // Test dependency collection via legacy HttpWebRequest async.
        private static async Task RequestGoogleHomPageViaHttpWebRequestLegacyAsync()
        {
            var request = (HttpWebRequest)WebRequest.Create($"http://www.google.com/?async");

            using var response = await request.GetResponseAsync().ConfigureAwait(false);
        }

        // Test dependency collection via legacy HttpWebRequest IAsyncResult.
        private static void RequestGoogleHomPageViaHttpWebRequestLegacyAsyncResult()
        {
            var request = (HttpWebRequest)WebRequest.Create($"http://www.google.com/?async");

            var asyncResult = request.BeginGetResponse(null, null);

            using var response = request.EndGetResponse(asyncResult);
        }
    }
}
