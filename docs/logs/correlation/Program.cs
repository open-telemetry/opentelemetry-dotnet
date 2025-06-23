// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

public static class Program
{
    private static readonly ActivitySource MyActivitySource = new("MyCompany.MyProduct.MyLibrary");

    public static void Main()
    {
        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("MyCompany.MyProduct.MyLibrary")
            .AddConsoleExporter()
            .Build();

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(logging =>
            {
                logging.AddConsoleExporter();
            });
        });

        var logger = loggerFactory.CreateLogger<Program>();

        using (var activity = MyActivitySource.StartActivity("SayHello"))
        {
            // Write a log within the context of an activity
            logger.FoodPriceChanged("artichoke", 9.99);
        }

        // Dispose logger factory before the application ends.
        // This will flush the remaining logs and shutdown the logging pipeline.
        loggerFactory.Dispose();

        // Dispose tracer provider before the application ends.
        // This will flush the remaining spans and shutdown the tracing pipeline.
        tracerProvider.Dispose();
    }
}
