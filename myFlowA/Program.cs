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
using OpenTelemetry;
using OpenTelemetry.Trace;
using Microsoft.Extensions.Logging;

public class Program
{
    private static readonly ActivitySource DemoSource = new ActivitySource("OTel.Demo");

    public static void Main()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder
                .AddOpenTelemetry(options =>
                {
                    options.AddMyLogExporter();
                    //options.AddProcessor(new BatchLogExportProcessor<LogRecord>());
                    /* what the extension is doing:
                     * AddProcessor(new BatchProcessor<LogRecord>(new FilterExporter(rules, new GenevaExporter(...))))
                     * */

                }));

        var logger = loggerFactory.CreateLogger<Program>();
        logger.LogInformation("HTTP POST {url}.",
            "https://test.core.windows.net/foo/bar?sig=abcdefghijklmnopqrstuvwxyz0123456789%2F%2BABCDE%3D");

    }
}
