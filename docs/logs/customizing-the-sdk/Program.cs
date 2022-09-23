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
            builder
                .AddOpenTelemetry(options => options.IncludeScopes = true)
                .ConfigureResource(r => r.AddService(serviceName: "MyService", serviceVersion: "1.0.0"))
                .AddConsoleExporter();
        });

        var logger = loggerFactory.CreateLogger<Program>();

        logger.LogInformation("Hello Information");
        logger.LogWarning("Hello Warning");
        logger.LogError("Hello Error");

        // log with scopes
        using (logger.BeginScope("operation"))
        using (logger.BeginScope("hardware"))
        {
            logger.LogError("{name} is broken.", "refrigerator");
        }
    }
}
