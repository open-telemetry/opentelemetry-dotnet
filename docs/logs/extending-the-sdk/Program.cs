// <copyright file="Program.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OpenTelemetry;

namespace ExtendingTheSdk;

public class Program
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
    }

    internal struct Food
    {
        public string Name { get; set; }

        public double Price { get; set; }
    }
}
