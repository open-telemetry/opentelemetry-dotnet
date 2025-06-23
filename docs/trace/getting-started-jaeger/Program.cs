// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace GettingStartedJaeger;

internal static class Program
{
    private static readonly ActivitySource MyActivitySource = new("OpenTelemetry.Demo.Jaeger");

    public static async Task Main()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(
                serviceName: "DemoApp",
                serviceVersion: "1.0.0"))
            .AddSource("OpenTelemetry.Demo.Jaeger")
            .AddHttpClientInstrumentation()
            .AddConsoleExporter()
            .AddOtlpExporter()
            .Build();

        using var parent = MyActivitySource.StartActivity("JaegerDemo");

        using (var client = new HttpClient())
        {
            using (var slow = MyActivitySource.StartActivity("SomethingSlow"))
            {
                await client.GetStringAsync(new Uri("https://httpstat.us/200?sleep=1000")).ConfigureAwait(false);
                await client.GetStringAsync(new Uri("https://httpstat.us/200?sleep=1000")).ConfigureAwait(false);
            }

            using (var fast = MyActivitySource.StartActivity("SomethingFast"))
            {
                await client.GetStringAsync(new Uri("https://httpstat.us/301")).ConfigureAwait(false);
            }
        }
    }
}
