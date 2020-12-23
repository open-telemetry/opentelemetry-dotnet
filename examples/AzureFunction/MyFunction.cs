// <copyright file="MyFunction.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;

namespace Examples.AzureFunction
{
    public class MyFunction
    {
        private static ActivitySource activitySource = new ActivitySource("MyFunction");

        [FunctionName("MyFunction")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ExecutionContext context)
        {
            using var activity = activitySource.StartActivity("MyFunction", ActivityKind.Server);
            activity?.SetTag("faas.trigger", "http");

            // TODO: Consider adding other relevant tags to the activity demonstrating the FAAS semantic conventions:
            // https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/semantic_conventions/faas.md

            // TODO: Figure out why this HttpClient invocation does not generate an activity.
            using var client = new HttpClient();
            var response = await client.GetStringAsync("https://opentelemetry.io/");
            return new OkObjectResult(response);
        }
    }
}
