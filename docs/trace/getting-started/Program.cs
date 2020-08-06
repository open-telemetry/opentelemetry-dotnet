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
using OpenTelemetry.Exporter.Console;
using OpenTelemetry.Trace;

public class Program
{
    private static readonly ActivitySource MyActivitySource = new ActivitySource(
        "MyCompany.MyProduct.MyLibrary");

    public static void Main()
    {
        /* making it easier for the user:

        using var tracerProvider = Sdk.CreateTracerProvider()
            .AddProcessor(new SimpleActivityProcessor(new ConsoleExporter()));

        using var tracerProvider = Sdk.CreateTracerProvider()
            .AddConsoleExporter();

        1) AddConsoleExporter is an extension function provided by ConsoleExporter
        2) individual exporter should pick the right (batching vs. simple) processor by default
        3) individual exporter can expose an option letting people to use a non-default processor
        4) AddConsoleExporter(options = null) should use the default option
        */
        using var tracerProvider = Sdk.CreateTracerProvider(
            new string[]
            {
                "MyCompany.MyProduct.MyLibrary",
                "MyCompany.AnotherProduct.*", // I think it'll be nice to support wildcard
            })
            .AddProcessor(new SimpleActivityProcessor(new ConsoleExporter(new ConsoleExporterOptions())));

        // processor can be added on-the-fly according to the spec (MAY instead of MUST)
        // https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/sdk.md#tracer-creation
        tracerProvider.AddProcessor(new SimpleActivityProcessor(new ConsoleExporter(new ConsoleExporterOptions())));

        using (var activity = MyActivitySource.StartActivity("SayHello"))
        {
            activity?.SetTag("foo", 1);
            activity?.SetTag("bar", "Hello, World!");
            activity?.SetTag("baz", new int[] { 1, 2, 3 });
        }
    }
}
