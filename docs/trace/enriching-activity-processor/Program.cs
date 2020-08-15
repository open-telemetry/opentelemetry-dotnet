// <copyright file="Program.cs" company="OpenTelemetry Authors">
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

using System;
using System.Net.Http;
using System.Threading.Tasks;
using OpenTelemetry;
using OpenTelemetry.Trace;

public class Program
{
    private static readonly HttpClient HttpClient = new HttpClient();

    public static async Task Main()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddHttpClientInstrumentation()
            .AddProcessor(new EnrichingActivityProcessor())
            .AddConsoleExporter()
            .Build();

        using (EnrichmentScope.Begin(a =>
        {
            a.AddTag("mycompany.user_id", 1234);
            a.AddTag("mycompany.customer_id", 5678);

            HttpRequestMessage request = (HttpRequestMessage)a.GetCustomProperty("HttpHandler.Request");
            if (request != null)
            {
                a.AddTag("http.user_agent", request.Headers.UserAgent.ToString());
            }

            HttpResponseMessage response = (HttpResponseMessage)a.GetCustomProperty("HttpHandler.Response");
            if (response != null)
            {
                a.AddTag("http.content_type", response.Content.Headers.ContentType.ToString());
            }
        }))
        {
            using var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri("https://www.opentelemetry.io/"),
            };

            request.Headers.UserAgent.TryParseAdd("mycompany/mylibrary");

            using var response = await HttpClient.SendAsync(request).ConfigureAwait(false);
        }
    }
}
