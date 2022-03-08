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

using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using OpenTelemetry.Logs;

public class Program
{
    private static async Task Main(string[] args)
    {
        // Note: CreateDefaultBuilder() initializes a new instance of the
        // Microsoft.Extensions.Hosting.HostBuilder class with
        // pre-configured defaults. These defaults initialize ILogger and
        // configures this program to read the appsettings.json config file.
        await Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                services.AddHostedService<ConsoleHostedService>();
            })
            .ConfigureLogging(builder =>
            {
                // Note: this filter overrides those set in the appsettings.json
                builder.AddFilter<OpenTelemetryLoggerProvider>(
                        category: "Program.ConsoleHostedService",
                        level: LogLevel.Error);

                builder.AddOpenTelemetry(options => options.AddConsoleExporter());
            })
            .RunConsoleAsync();
    }

    internal sealed class ConsoleHostedService : IHostedService
    {
        private readonly ILogger logger;

        public ConsoleHostedService(ILogger<ConsoleHostedService> logger)
        {
            this.logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            this.logger.LogInformation("Hello Information");
            this.logger.LogWarning("Hello Warning");
            this.logger.LogError("Hello Error");

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
