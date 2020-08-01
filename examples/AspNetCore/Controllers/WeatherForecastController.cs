using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Examples.AspNetCore.Models;
using Microsoft.AspNetCore.Mvc;

namespace Examples.AspNetCore.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private static HttpClient httpClient = new HttpClient();

        [HttpGet]
        public IEnumerable<WeatherForecast> Get()
        {
            // Making an http call here to serve as an example of
            // how dependency calls will be captured and treated
            // automatically as child of incoming request.
            var res = httpClient.GetStringAsync("http://google.com").Result;
            var rng = new Random();
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = rng.Next(-20, 55),
                Summary = Summaries[rng.Next(Summaries.Length)]
            })
            .ToArray();
        }
    }
}
