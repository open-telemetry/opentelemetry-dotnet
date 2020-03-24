using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

using OpenTelemetry.Exporter.AspNet.Models;

namespace OpenTelemetry.Exporter.AspNet.Controllers
{
    public class WeatherForecastController : ApiController
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        [HttpGet] // For testing traditional routing. Ex: https://localhost:XXXX/api/weatherforecast
        public IEnumerable<WeatherForecast> Get()
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

        [Route("subroute/{customerId}")] // For testing attribute routing. Ex: https://localhost:XXXX/subroute/10
        [HttpGet]
        public IEnumerable<WeatherForecast> Get(int customerId)
        {
            if (customerId < 0)
            {
                throw new ArgumentException();
            }

            return this.Get();
        }
    }
}
