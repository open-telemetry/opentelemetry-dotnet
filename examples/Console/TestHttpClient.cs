// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Examples.Console;

internal class TestHttpClient
{
    // To run this example, run the following command from
    // the reporoot\examples\Console\.
    // (eg: C:\repos\opentelemetry-dotnet\examples\Console\)
    //
    // dotnet run httpclient
    internal static int Run(HttpClientOptions options)
    {
        Debug.Assert(options != null, "options was null");

        System.Console.WriteLine("Hello World!");

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddHttpClientInstrumentation()
            .ConfigureResource(r => r.AddService("http-service-example"))
            .AddSource("http-client-test")
            .AddConsoleExporter()
            .Build();

        using var source = new ActivitySource("http-client-test");
        using (var parent = source.StartActivity("incoming request", ActivityKind.Server))
        {
            using var client = new HttpClient();
            client.GetStringAsync("http://bing.com").GetAwaiter().GetResult();
        }

        System.Console.WriteLine("Press Enter key to exit.");
        System.Console.ReadLine();

        return 0;
    }
}
