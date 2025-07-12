// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace GettingStartedConsole;

internal static class Program
{
    private static readonly ActivitySource MyActivitySource = new("MyCompany.MyProduct.MyLibrary");

    public static void Main()
    {
        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("MyCompany.MyProduct.MyLibrary")
            .AddConsoleExporter()
            .Build();

        using (var activity = MyActivitySource.StartActivity("SayHello"))
        {
            int[] intArray = [1, 2, 3];
            activity?.SetTag("foo", 1);
            activity?.SetTag("bar", "Hello, World!");
            activity?.SetTag("baz", intArray);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }

        // Dispose tracer provider before the application ends.
        // This will flush the remaining spans and shutdown the tracing pipeline.
        tracerProvider.Dispose();
    }
}
