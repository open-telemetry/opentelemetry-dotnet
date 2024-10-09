// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;

public class Program
{
    private static readonly ActivitySource MyActivitySource = new("MyCompany.MyProduct.MyLibrary");

    public static void Main()
    {
        // Initialize the tracerProvider
        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("MyCompany.MyProduct.MyLibrary")
            .AddConsoleExporter()
            .Build();

        // Create a new activity using the ActivitySource
        var activity = MyActivitySource.StartActivity("SayHello");

        if (activity != null)
        {
            activity.SetTag("foo", 1);
            activity.SetTag("bar", "Hello, World!");
            activity.SetTag("baz", new int[] { 1, 2, 3 });
            activity.SetStatus(ActivityStatusCode.Ok);

            // End the activity (implicitly done when activity goes out of scope)
        }

        // Dispose the tracerProvider explicitly to ensure cleanup
        // and proper flushing of any telemetry data before the app exits
        tracerProvider.Dispose();
    }
}
