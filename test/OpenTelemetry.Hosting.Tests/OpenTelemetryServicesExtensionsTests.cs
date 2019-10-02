// <copyright file="StackExchangeRedisCallsCollectorTests.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Hosting
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Config;
    using OpenTelemetry.Trace.Export;
    using OpenTelemetry.Trace.Sampler;
    using Xunit;

    public class OpenTelemetryServicesExtensionsTests
    {
        [Fact]
        public void AddOpenTelemetry_NoBuilder_DefaultsRegisteredInDI()
        {
            var services = new ServiceCollection();

            services.AddOpenTelemetry();

            var serviceProvider = services.BuildServiceProvider();

            Assert.IsType<NoopTracer>(serviceProvider.GetRequiredService<ITracer>());
            Assert.IsType<BatchingSpanProcessor>(serviceProvider.GetRequiredService<SpanProcessor>());
            Assert.Same(Samplers.AlwaysSample, serviceProvider.GetRequiredService<ISampler>());
        }

        [Fact]
        public void AddOpenTelemetry_AddCollector_CollectorRegisteredInDI()
        {
            var services = new ServiceCollection();

            services.AddOpenTelemetry(telemetry =>
            {
                telemetry.AddCollector<TestCollector>();
            });

            var serviceProvider = services.BuildServiceProvider();

            var collector = serviceProvider.GetRequiredService<TestCollector>();
            Assert.NotNull(collector);
        }

        [Fact]
        public void AddOpenTelemetry_AddCollectorWithOptions_CollectorRegisteredInDI()
        {
            var options = new TestOptions();

            var services = new ServiceCollection();

            services.AddOpenTelemetry(telemetry =>
            {
                telemetry.AddCollector<TestCollectorWithOptions>(options);
            });

            var serviceProvider = services.BuildServiceProvider();

            var collector = serviceProvider.GetRequiredService<TestCollectorWithOptions>();
            Assert.NotNull(collector);
            Assert.Same(options, collector.Options);
        }

        [Fact]
        public void AddOpenTelemetry_SetSpanExporter_SpanExporterRegisteredInDI()
        {
            var services = new ServiceCollection();

            services.AddOpenTelemetry(telemetry =>
            {
                telemetry.SetSpanExporter<TestSpanExporer>();
            });

            var serviceProvider = services.BuildServiceProvider();

            Assert.IsType<TestSpanExporer>(serviceProvider.GetRequiredService<SpanExporter>());
        }

        [Fact]
        public void AddOpenTelemetry_SetSpanExporterWithOptions_SpanExporterRegisteredInDI()
        {
            var options = new TestOptions();

            var services = new ServiceCollection();

            services.AddOpenTelemetry(telemetry =>
            {
                telemetry.SetSpanExporter<TestSpanExporerWithOptions>(options);
            });

            var serviceProvider = services.BuildServiceProvider();

            var exporter = Assert.IsType<TestSpanExporerWithOptions>(serviceProvider.GetRequiredService<SpanExporter>());
            Assert.Same(options, exporter.Options);
        }

        [Fact]
        public void AddOpenTelemetry_SetTracerWithOptions_TracerRegisteredInDI()
        {
            var services = new ServiceCollection();

            services.AddOpenTelemetry(telemetry =>
            {
                telemetry.SetTracer<Tracer, TraceConfig>();
            });

            var serviceProvider = services.BuildServiceProvider();

            Assert.IsType<Tracer>(serviceProvider.GetRequiredService<ITracer>());
        }

        internal class TestSpanExporer : SpanExporter
        {
            public override Task<ExportResult> ExportAsync(IEnumerable<Span> batch, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public override Task ShutdownAsync(CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }

        internal class TestSpanExporerWithOptions : SpanExporter
        {
            public TestOptions Options { get; }

            public TestSpanExporerWithOptions(TestOptions options)
            {
                Options = options;
            }

            public override Task<ExportResult> ExportAsync(IEnumerable<Span> batch, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public override Task ShutdownAsync(CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }

        internal class TestCollector : IDisposable
        {
            public bool Disposed { get; private set; }

            public void Dispose()
            {
                Disposed = true;
            }
        }

        internal class TestCollectorWithOptions : IDisposable
        {
            public bool Disposed { get; private set; }
            public TestOptions Options { get; }

            public TestCollectorWithOptions(TestOptions options)
            {
                Options = options;
            }

            public void Dispose()
            {
                Disposed = true;
            }
        }

        internal class TestOptions
        {

        }
    }
}
