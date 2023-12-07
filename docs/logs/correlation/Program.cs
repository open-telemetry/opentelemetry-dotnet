// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace Correlation;

public class Program
{
    private static readonly ActivitySource MyActivitySource = new(
        "MyCompany.MyProduct.MyLibrary");

    public static void Main()
    {
        // Setup Logging
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.AddConsoleExporter();
            });
        });

        var logger = loggerFactory.CreateLogger<Program>();

        // Setup Traces
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("MyCompany.MyProduct.MyLibrary")
            .AddConsoleExporter()
            .Build();

        // Emit activity
        using (var activity = MyActivitySource.StartActivity("SayHello"))
        {
            activity?.SetTag("foo", 1);

            // emit logs within the context
            // of activity
            logger.LogInformation("Hello from {name} {price}.", "tomato", 2.99);
        }
    }
}
