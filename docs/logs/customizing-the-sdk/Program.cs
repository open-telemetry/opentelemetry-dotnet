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

public class Program
{
    public static void Main()
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.AddOpenTelemetry(options =>
            {
                options.IncludeScopes = false;
                options.IncludeFormattedMessage = false;
                options.ParseStateValues = false;
                options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(
                        serviceName: "MyService",
                        serviceVersion: "1.0.0"
                        ));
                // options.AddProcessor(new MyProcessor("MyProcessor")); // excluded from this example. Please see "extending-the-sdk".
                options.AddConsoleExporter();
            });
        });

        var logger = loggerFactory.CreateLogger<Program>();

        logger.LogInformation("Hello Information");
        logger.LogWarning("Hello Warning");
        logger.LogError("Hello Error");

        // **TESTING options.IncludeFormattedMesasge and options.ParseStateValues
        logger.LogInformation("Hello from {name} {price}.", "tomato", 2.99);

        // **TESTING options.IncludeScopes
        using (logger.BeginScope("My Scope 1"))
        using (logger.BeginScope("My Scope 2"))
        {
            logger.LogInformation("Hello Information within scope");
        }

        loggerFactory.Dispose(); // flush
    }
}
