// <copyright file="ForwardController.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

namespace TestApp.AspNetCore._2._0.Controllers
{
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Newtonsoft.Json;

    [Route("api/[controller]")]
    public class ForwardController : Controller
    {
        private readonly HttpClient httpClient;
        public ForwardController(HttpClient httpclient)
        {
            this.httpClient = httpclient;
        }

        private async Task<string> CallNextAsync(string url, Data[] arguments)
        {
            if (url != null)
            {
                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(arguments), Encoding.UTF8, "application/json"),
                };
                var response = await httpClient.SendAsync(request);
                return await response.Content.ReadAsStringAsync();
            }

            return "all done";
        }

        // POST api/values
        [HttpPost]
        public async Task<string> Post([FromBody]Data[] data)
        {
            var result = string.Empty;

            if (data != null)
            {
                foreach (var argument in data)
                {
                    if (argument.sleep != null)
                    {
                        result = "slept for " + argument.sleep.Value + " ms";
                        await Task.Delay(argument.sleep.Value);
                    }

                    result += await CallNextAsync(argument.url, argument.arguments);
                }
            }
            else
            {
                result = "done";
            }

            return result;
        }
    }

    public class Data
    {
        public int? sleep { get; set; }
        public string url { get; set; }
        public Data[] arguments { get; set; }
    }
}