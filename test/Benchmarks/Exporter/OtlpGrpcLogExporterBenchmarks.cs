// <copyright file="OtlpGrpcLogExporterBenchmarks.cs" company="OpenTelemetry Authors">
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

extern alias OpenTelemetryProtocol;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetryProtocol::OpenTelemetry.Exporter;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OtlpCollector = OpenTelemetryProtocol::Opentelemetry.Proto.Collector.Logs.V1;
using OtlpLogs = OpenTelemetryProtocol::Opentelemetry.Proto.Logs.V1;

namespace Benchmarks.Exporter
{
    [MemoryDiagnoser]
    public class OtlpGrpcLogExporterBenchmarks
    {
        private readonly Func<CustomReadOnlyListState, Exception, string> messageFormatter = (s, e) => s.ToString();
        private ILoggerFactory loggerFactory;
        private ILogger logger;

        [Params(10, 50, 100)]
        public int NumberOfLogs { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            OtlpExporterOptions options = new();

            this.loggerFactory = LoggerFactory.Create(configure => configure
                .AddOpenTelemetry(builder =>
                {
                    builder.ParseStateValues = true;
                    builder.IncludeScopes = true;

                    builder.AddProcessor(
                        LogRecordExtensions.ToOtlpLog,
                        new SimpleExportProcessor<OtlpLogs.LogRecord>(new OtlpLogExporter(options, new NoopExportClient())));
                }));

            this.logger = this.loggerFactory.CreateLogger<OtlpGrpcLogExporterBenchmarks>();
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            this.loggerFactory.Dispose();
        }

        [Benchmark]
        public void OtlpLogExporterr_Batching()
        {
            for (int i = 0; i < this.NumberOfLogs; i++)
            {
                if (i % 10 == 1)
                {
                    this.logger.Log(LogLevel.Information, 0, new CustomReadOnlyListState(i), null, this.messageFormatter);
                }
                else if (i % 10 == 2)
                {
                    using IDisposable scope = this.logger.BeginScope("Scope {Index}", i);

                    this.logger.LogInformation("Log scope message {Index}.", i);
                }
                else if (i % 10 == 3)
                {
                    using IDisposable scope = this.logger.BeginScope(new CustomReadOnlyListState(i));

                    this.logger.Log(LogLevel.Information, 0, new CustomReadOnlyListState(i), null, this.messageFormatter);
                }
                else
                {
                    this.logger.LogInformation("Log message {Index}.", i);
                }
            }
        }

        private readonly struct CustomReadOnlyListState : IReadOnlyList<KeyValuePair<string, object>>
        {
            private readonly int index;

            public CustomReadOnlyListState(int index)
            {
                this.index = index;
            }

            public int Count => 1;

            public KeyValuePair<string, object> this[int index]
                => new("Index", this.index);

            public override string ToString() => $"Log message custom scope {this.index}";

            public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
            {
                for (var i = 0; i < this.Count; i++)
                {
                    yield return this[i];
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
        }

        private sealed class NoopExportClient : IExportClient<OtlpCollector.ExportLogsServiceRequest>
        {
            public bool SendExportRequest(OtlpCollector.ExportLogsServiceRequest request, CancellationToken cancellationToken = default)
            {
                return true;
            }

            public bool Shutdown(int timeoutMilliseconds)
            {
                return true;
            }
        }
    }
}
