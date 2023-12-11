// <copyright file="Meat.cs" company="OpenTelemetry Authors">
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
using System.Runtime.CompilerServices;
#if NET6_0_OR_GREATER
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
#endif
using OpenTelemetry.Trace;

namespace OpenTelemetry.Tests.Stress;

public partial class Program
{
    public static TracerProvider? TracerProvider;
    public static ActivitySource? ActivitySource = new ActivitySource("MySource");
#if NET6_0_OR_GREATER
    private static WebApplication? app;
    private static HttpClient? httpClient;
#endif

    public static void Main()
    {
#if NET6_0_OR_GREATER
        TracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAspNetCoreInstrumentation()
            .Build();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        var app = builder.Build();
        app.MapGet("/", async context => await context.Response.WriteAsync($"Hello World!"));
        app.RunAsync();

        Program.app = app;
        httpClient = new HttpClient();
#endif
        Stress(concurrency: 0, prometheusPort: 9464);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void Run()
    {
#if NET6_0_OR_GREATER
        if (httpClient != null)
        {
            var message = new HttpRequestMessage();
            message.Method = HttpMethod.Get;
            message.RequestUri = new Uri("http://localhost:5000");
            var httpResponse = httpClient.Send(message);
            httpResponse?.EnsureSuccessStatusCode();
        }
#endif
    }
}
