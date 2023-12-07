// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace CustomizingTheSdk;

public class Program
{
    public static void Main()
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.IncludeScopes = true;
                options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(
                    serviceName: "MyService",
                    serviceVersion: "1.0.0"));
                options.AddConsoleExporter();
            });
        });

        var logger = loggerFactory.CreateLogger<Program>();

        logger.LogInformation("Hello from {name} {price}.", "tomato", 2.99);
        logger.LogWarning("Hello from {name} {price}.", "tomato", 2.99);
        logger.LogError("Hello from {name} {price}.", "tomato", 2.99);

        // log with scopes
        using (logger.BeginScope(new List<KeyValuePair<string, object>>
        {
            new KeyValuePair<string, object>("store", "Seattle"),
        }))
        {
            logger.LogInformation("Hello from {food} {price}.", "tomato", 2.99);
        }
    }
}
