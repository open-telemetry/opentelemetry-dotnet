// <copyright file="TestHttpClient.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Examples.Console
{
    internal class TestHttpClient
    {
        internal static object Run()
        {
            System.Console.WriteLine("Hello World!");

            using var openTelemetry = Sdk.CreateTracerProvider(
                (builder) => builder.AddHttpClientInstrumentation()
                .SetResource(Resources.CreateServiceResource("http-service-example"))
                .AddActivitySource("http-client-test")
                .UseConsoleExporter(opt => opt.DisplayAsJson = false));

            var source = new ActivitySource("http-client-test");
            using (var parent = source.StartActivity("incoming request", ActivityKind.Server))
            {
                using var client = new HttpClient();
                client.GetStringAsync("http://bing.com").GetAwaiter().GetResult();
            }

            System.Console.ReadLine();

            return null;
        }
    }
}
