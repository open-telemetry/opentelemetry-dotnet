// <copyright file="TestHttpClient.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

namespace Samples
{
    using System;
    using System.Net.Http;
    using OpenTelemetry.Collector.Dependencies;
    using OpenTelemetry.Exporter.Zipkin;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Sampler;

    internal class TestHttpClient
    {
        private static readonly ITracer Tracer = Tracing.Tracer;

        internal static object Run()
        {
            Console.WriteLine("Hello World!");

            using (new DependenciesCollector(new DependenciesCollectorOptions(), Tracer, Samplers.AlwaysSample))
            {
                var exporter = new ZipkinTraceExporter(
                    new ZipkinTraceExporterOptions()
                    {
                        Endpoint = new Uri("https://zipkin.azurewebsites.net/api/v2/spans"),
                        ServiceName = typeof(Program).Assembly.GetName().Name,
                    },
                    Tracing.ExportComponent);
                exporter.Start();

                using (Tracer.WithSpan(Tracer.SpanBuilder("incoming request").SetSampler(Samplers.AlwaysSample).StartSpan()))
                {
                    using (var client = new HttpClient())
                    {
                        client.GetStringAsync("http://bing.com").GetAwaiter().GetResult();
                    }
                }

                Console.ReadLine();

                return null;
            }
        }
    }
}
