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

using OpenTelemetry.Trace;
#if NETCOREAPP2_1
using Microsoft.Extensions.DependencyInjection;
#endif
using Microsoft.Extensions.Logging;

public class Program
{
    public static void Main()
    {
#if NETCOREAPP2_1
        var serviceCollection = new ServiceCollection().AddLogging(builder =>
#else
        using var loggerFactory = LoggerFactory.Create(builder =>
#endif
        {
            builder.AddOpenTelemetry(options => options
                .AddConsoleExporter());
        });

#if NETCOREAPP2_1
        using var serviceProvider = serviceCollection.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
#else
        var logger = loggerFactory.CreateLogger<Program>();
#endif

        logger.LogInformation("Hello from {name} {price}.", "tomato", 2.99);
    }
}
