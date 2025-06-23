// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry;

namespace ExtendingTheSdk;

public static class Program
{
    public static void Main()
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddOpenTelemetry(options =>
            {
                options.IncludeScopes = true;
                options.AddProcessor(new MyProcessor("ProcessorA"))
                       .AddProcessor(new MyProcessor("ProcessorB"))
                       .AddProcessor(new SimpleLogRecordExportProcessor(new MyExporter("ExporterX")))
                       .AddMyExporter();
            }));

        var logger = loggerFactory.CreateLogger<Program>();

        // unstructured log
        logger.LogInformation("Hello, World!");

        // String interpolation, as in the below line, results in unstructured logging, and is not recommended
        // logger.LogInformation($"Hello from potato {0.99}.");

        // structured log with template
        logger.LogInformation("Hello from {name} {price}.", "tomato", 2.99);

        // structured log with strong type
        logger.LogInformation("{food}", new Food { Name = "artichoke", Price = 3.99 });

        // structured log with anonymous type
        logger.LogInformation("{food}", new { Name = "pumpkin", Price = 5.99 });

        // structured log with general type
        logger.LogInformation("{food}", new Dictionary<string, object>
        {
            ["Name"] = "truffle",
            ["Price"] = 299.99,
        });

        // log with scopes
        using (logger.BeginScope("[operation]"))
        using (logger.BeginScope("[hardware]"))
        {
            logger.LogError("{name} is broken.", "refrigerator");
        }

        // message will be redacted by MyRedactionProcessor
        logger.LogInformation("OpenTelemetry {sensitiveString}.", "<secret>");
    }

    internal struct Food
    {
        public string Name { get; set; }

        public double Price { get; set; }
    }
}
