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
using OpenTelemetry.Trace.Samplers;

public class Program
{
    private static readonly ActivitySource MyActivitySource = new ActivitySource(
        "MyCompany.MyProduct.MyLibrary");

    public static void Main()
    {
        /* a simple case, where default ALWAYS_ON sampler is used:
        using var tracerProvider = Sdk.CreateTracerProvider()
            .AddProcessor(new BatchingActivityProcessor(new ZipkinExporter()));
        */

        /* making it easier for the user:
        using var tracerProvider = Sdk.CreateTracerProvider()
            .AddZipkinExporter(options);

        1) AddExporter should be an extension function provided by individual exporter
        2) individual exporter should pick the right (batching vs. simple) processor by default
        3) individual exporter can expose an option letting people to use a non-default processor
        */

        using var tracerProvider = Sdk.CreateTracerProvider(
            new string[]
            {
                "MyCompany.MyProduct.MyLibrary",
            },
            new ProbabilitySampler(0.5))
            .AddProcessor(new MyActivityProcessor("A"))
            .AddProcessor(new MyActivityProcessor("B"));

        using (var activity = MyActivitySource.StartActivity("Foo"))
        {
        }

        tracerProvider.AddProcessor(new MyActivityProcessor("C"));

        using (var activity = MyActivitySource.StartActivity("Bar"))
        {
        }

        /* according to the spec, processors can be added at runtime
        tracerProvider.AddProcessor(new MultiActivityProcessor(
            [processor1, processor2, processor3]
        ));
        */

        /* existing samplers can be reused as a tail sampling filter
        tracerProvider.AddProcessor(new MultiActivityProcessor(
            [new TailSamplingProcessor(new Sampler()), new SimpleExportProcessor(new ConsoleExporter())]
        ));
        */
    }
}
