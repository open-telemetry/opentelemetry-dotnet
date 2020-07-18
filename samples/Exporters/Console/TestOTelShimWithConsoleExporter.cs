﻿// <copyright file="TestOTelShimWithConsoleExporter.cs" company="OpenTelemetry Authors">
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

using System;
using System.Diagnostics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;

namespace Samples
{
    internal class TestOTelShimWithConsoleExporter
    {
        internal static object Run(OpenTelemetryShimOptions options)
        {
            var originalAct = new Activity("original").Start();
            var cur = Activity.Current;

            var newAct = new Activity("newone").Start();

            Activity.Current = cur;
            cur = Activity.Current;

            // Enable OpenTelemetry for the source "MyCompany.MyProduct.MyWebServer"
            // and use Console exporter.
            using var openTelemetry = OpenTelemetrySdk.EnableOpenTelemetry(
                (builder) => builder.AddActivitySource("MyCompany.MyProduct.MyWebServer")
                    .SetResource(Resources.CreateServiceResource("MyServiceName"))
                    .UseConsoleExporter(opt => opt.DisplayAsJson = options.DisplayAsJson));

            // The above line is required only in applications
            // which decide to use OpenTelemetry.

            // Following shows how to use the OpenTelemetryShim, which is a thin layer on top
            // of Activity and associated classes.
            var tracer = TracerProvider.GetTracer("MyCompany.MyProduct.MyWebServer");
            var span = tracer.StartSpan("parent span");
            span.SetAttribute("my", "value");
            span.UpdateName("parent span new name");

            var spanChild = tracer.StartSpan("child span");
            spanChild.SetAttribute("ch", "value");
            spanChild.End();

            span.End();

            Console.WriteLine("Press Enter key to exit.");

            return null;
        }
    }
}
