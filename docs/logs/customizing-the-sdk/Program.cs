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
                options.ConfigureResource(r => r.AddService(serviceName: "MyService", serviceVersion: "1.0.0"));
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
