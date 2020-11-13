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

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Trace;

public class Program
{
    private static readonly ActivitySource DemoSource = new ActivitySource("OTel.Demo");

    public static void Main()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("OTel.Demo")
            .Build();

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
            .AddJsonConsole(options => { options.IncludeScopes = true; })
            .Configure(options => options.ActivityTrackingOptions =
                ActivityTrackingOptions.TraceId |
                ActivityTrackingOptions.SpanId);
        });

        var logger = loggerFactory.CreateLogger<Program>();

        logger.LogInformation("Hello, World!");

        using (var activity = DemoSource.StartActivity("Foo"))
        {
            logger.LogInformation("Hello, World!");
        }
    }
}
