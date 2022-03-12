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
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ExtendingTheSdk;

public class Program
{
    private static readonly ActivitySource DemoSource = new("OTel.Demo");

    public static void Main()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new MySampler())
            .AddSource("OTel.Demo")
            .SetResourceBuilder(ResourceBuilder.CreateEmpty().AddDetector(new MyResourceDetector()))
            .AddProcessor(new MyProcessor("ProcessorA"))
            .AddProcessor(new MyProcessor("ProcessorB"))
            .AddProcessor(new SimpleActivityExportProcessor(new MyExporter("ExporterX")))
            .AddMyExporter()
            .Build();

        using var foo = DemoSource.StartActivity("Foo");
        using var bar = DemoSource.StartActivity("Bar");
        using var baz = DemoSource.StartActivity("Baz");
    }
}
