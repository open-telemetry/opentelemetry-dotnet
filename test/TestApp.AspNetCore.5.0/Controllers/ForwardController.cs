// <copyright file="ForwardController.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
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

using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace TestApp.AspNetCore._5._0.Controllers
{
    [Route("api/[controller]")]
    public class ForwardController : Controller
    {
        private readonly HttpClient httpClient;

        public ForwardController(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        // POST api/test
        [HttpPost]
        public async Task<string> Post([FromBody] Data[] data)
        {
            var result = string.Empty;

            if (data != null)
            {
                foreach (var argument in data)
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, argument.Url)
                    {
                        Content = new StringContent(
                            JsonConvert.SerializeObject(argument.Arguments),
                            Encoding.UTF8,
                            "application/json"),
                    };
                    await this.httpClient.SendAsync(request);
                }
            }
            else
            {
                result = "done";
            }

            return result;
        }

        public class Data
        {
            [JsonProperty("url")]
            public string Url { get; set; }

            [JsonProperty("arguments")]
            public Data[] Arguments { get; set; }
        }
    }
}
