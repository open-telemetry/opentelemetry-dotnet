// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry;

namespace ExtendingTheSdk;

internal static class Program
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
#pragma warning disable CA1848 // Use the LoggerMessage delegates
        logger.LogInformation("Hello, World!");

        // String interpolation, as in the below line, results in unstructured logging, and is not recommended
        // logger.LogInformation($"Hello from potato {0.99}.");

        // structured log with template
        logger.LogInformation("Hello from {Name} {Price}.", "tomato", 2.99);

        // structured log with strong type
        logger.LogInformation("{Food}", new Food { Name = "artichoke", Price = 3.99 });

        // structured log with anonymous type
        logger.LogInformation("{Food}", new { Name = "pumpkin", Price = 5.99 });

        // structured log with general type
        logger.LogInformation("{Food}", new Dictionary<string, object>
        {
            ["Name"] = "truffle",
            ["Price"] = 299.99,
        });

        // log with scopes
        using (logger.BeginScope("[operation]"))
        using (logger.BeginScope("[hardware]"))
        {
            logger.LogError("{Name} is broken.", "refrigerator");
        }

        // message will be redacted by MyRedactionProcessor
        logger.LogInformation("OpenTelemetry {SensitiveString}.", "<secret>");
#pragma warning restore CA1848 // Use the LoggerMessage delegates
    }

    internal struct Food
    {
        public string Name { get; set; }

        public double Price { get; set; }
    }
}
