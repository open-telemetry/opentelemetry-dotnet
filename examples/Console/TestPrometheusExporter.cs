// <copyright file="TestPrometheusExporter.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Examples.Console
{
    internal class TestPrometheusExporter
    {
        internal static async Task<object> RunAsync(int port, int pushIntervalInSecs, int totalDurationInMins)
        {
            System.Console.WriteLine($"OpenTelemetry Prometheus Exporter is making metrics available at http://localhost:{port}/metrics/");

            /*
            Following is sample prometheus.yml config. Adjust port,interval as needed.

            scrape_configs:
              # The job name is added as a label `job=<job_name>` to any timeseries scraped from this config.
              - job_name: 'OpenTelemetryTest'

                # metrics_path defaults to '/metrics'
                # scheme defaults to 'http'.

                static_configs:
                - targets: ['localhost:9184']
            */
            System.Console.WriteLine("Press Enter key to exit.");
            return null;
        }
    }
}
