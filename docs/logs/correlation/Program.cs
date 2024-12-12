// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

public class Program
{
    private static readonly ActivitySource MyActivitySource = new("MyCompany.MyProduct.MyLibrary");

    public static void Main()
    {
        var sdk = OpenTelemetrySdk.Create(builder => builder
            .WithLogging(logging => logging.AddConsoleExporter())
            .WithTracing(tracing =>
            {
                tracing.AddSource("MyCompany.MyProduct.MyLibrary");
                tracing.AddConsoleExporter();
            }));

        var logger = sdk.GetLoggerFactory().CreateLogger<Program>();

        using (var activity = MyActivitySource.StartActivity("SayHello"))
        {
            // Write a log within the context of an activity
            logger.FoodPriceChanged("artichoke", 9.99);
        }

        // Dispose SDK before the application ends.
        sdk.Dispose();
    }
}
