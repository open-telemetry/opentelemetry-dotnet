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
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

public class Program
{
    private static readonly ActivitySource MyActivitySource = new ActivitySource(
        "MyCompany.MyProduct.MyLibrary");

    public static void Main()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("MyCompany.MyProduct.MyLibrary")
            .AddConsoleExporter()
            .Build();

        /*
        # Proposed Design Principles

        * The API should be easy to use - for both application and library developers.
          * Instrumentation API - e.g. report an int number with few dimensions.
          * Configuration API - e.g. get the meter, configure the aggregation time
            window.
          * Consumption API - e.g. define a histogram.
        * Metrics should be reliable.
          * Not going to use unbounded resource.
          * Any time we callback to user code, we shouldn't take lock (be lock-free).
          * Metrics should be trust-worthy.
          * Correctness and fidelity. It doesn't mean that all the metrics data should
            be 100% accurate - sometimes from engineering perspective we might prefer an
            approximate value if it yields great performance/efficiency benefit. The
            minimum bar here is that the behavior should be well defined/documented and
            backed by mathematics.
          * In case the implementation needs to reduce data quality (e.g. throttle,
            resource cap, integer overflow, etc.), there should be a way for the
            consumer to know if the data should be trusted or not.
        * Metrics should be performant.
          * If most counter values could fit into 8-bit integer, we shouldn't start from
            allocating 16-bit.
          * All the hot-path should be lock-free - does not cause CPU context switch.
            The actual definition of "what are hot-paths" is subjective and should be
            hammered out as we started to build the PoC (for example, there could be
            batch updates for multiple metric data points which could take a lock).
        */

        MeterProvider.SetDefault(Sdk.CreateMeterProviderBuilder()
            /*
            .SetProcessor(processor)
            .SetExporter(exporter)
            */
            .SetPushInterval(TimeSpan.FromSeconds(1))
            .Build());

        var meterProvider = MeterProvider.Default;
        var meter = meterProvider.GetMeter("MyMeter");

        // https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/metrics/semantic_conventions/http-metrics.md#http-client
        var httpClientDuration = meter.CreateInt64Measure("http.client.duration");
        var httpClientError = meter.CreateInt64Counter("http.client.error");

        for (int i = 0; i < 10; i++)
        {
            using (var activity = MyActivitySource.StartActivity("HTTP GET"))
            {
                var httpVerb = "GET";
                var httpScheme = "https";
                var httpHost = "www.wikipedia.org";
                var httpPeerPort = 443;

                // how to prevent label creation and type conversion if the data is not needed by the listeners?
                var labels = new List<KeyValuePair<string, string>> {
                    new KeyValuePair<string, string>("http.method", httpVerb),
                    new KeyValuePair<string, string>("http.scheme", httpScheme),
                    new KeyValuePair<string, string>("http.host", httpHost),
                    new KeyValuePair<string, string>("http.peer.port", httpPeerPort.ToString(CultureInfo.InvariantCulture)),
                };

                try
                {
                    // make HTTP client call

                    var latencyMilliseconds = 60;
                    var httpStatusCode = 200;

                    activity?.SetTag("http.status_code", httpStatusCode);

                    labels.Add(new KeyValuePair<string, string>("http.status_code", httpStatusCode.ToString(CultureInfo.InvariantCulture)));

                    httpClientDuration.Record(default(SpanContext), latencyMilliseconds, meter.GetLabelSet(labels));

                    // https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/metrics/api.md#recordbatch-calling-convention
                    // if multiple measurements are reported using the same labels, a batch API would give better performance
                    // it is also possible to make atomic/transactional updates

                    /*
                    meter.RecordBatch(default(SpanContext), labels)
                         .Put(httpClientDuration, latencyMilliseconds)
                         .Put(httpClientError, 1)
                         .Record();
                    */
                }
                catch (Exception ex)
                {
                    // how to report the exception (supporting exemplars)?

                    httpClientError.Add(default(SpanContext), 1, meter.GetLabelSet(labels));
                }
            }
        }
    }
}
