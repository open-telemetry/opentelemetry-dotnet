﻿// <copyright file="Program.cs" company="OpenTelemetry Authors">
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
        using var tracerProvider = Sdk.CreateTracerProvider(new ProbabilitySampler(0.5))
            .AddListener("MyCompany.MyProduct.MyLibrary")
            .AddListener("MyCompany.AnotherProduct.*")
            .AddProcessor(new MyActivityProcessor("A"))
            .AddProcessor(new MyActivityProcessor("B"));

        using (var activity = MyActivitySource.StartActivity("Foo"))
        {
        }

        tracerProvider.AddProcessor(new MyActivityProcessor("C"));

        using (var activity = MyActivitySource.StartActivity("Bar"))
        {
        }

        /*
        tracerProvider.AddProcessor(new MultiActivityProcessor(
            [processor1, processor2, processor3]
        ));

        tracerProvider.AddProcessor(new MultiActivityProcessor(
            [new TailSamplingProcessor(new Sampler()), new SimpleExportProcessor(new ConsoleExporter())]
        ));
        */
    }
}
