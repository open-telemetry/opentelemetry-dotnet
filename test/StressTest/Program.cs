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

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace OpenTelemetry.StressTest
{
    internal class Program
    {
        private static ActivitySource source = new ActivitySource("OpenTelemetry.Exporter.Geneva.Stress");

        public static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            InitTraces();
            while (true)
            {
                RunTraces();
            }
        }

        private static void InitTraces()
        {
            OpenTelemetry.Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddSource("OpenTelemetry.Exporter.Geneva.Stress")
                .AddConsoleExporter()
                .Build();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void RunTraces()
        {
            using (var activity = source.StartActivity("Stress"))
            {
                activity?.SetTag("http.method", "GET");
                activity?.SetTag("http.url", "https://www.wikipedia.org/wiki/Rabbit");
                activity?.SetTag("http.status_code", 200);
            }
        }
    }
}
