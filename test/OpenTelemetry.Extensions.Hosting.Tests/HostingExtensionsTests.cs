// <copyright file="HostingExtensionsTests.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Extensions.Hosting.Tests
{
    public class HostingExtensionsTests
    {
        [Fact]
        public async Task AddOpenTelemetryTracerProviderInstrumentationCreationAndDisposal()
        {
            var testInstrumentation = new TestInstrumentation();
            var callbackRun = false;

            var builder = new HostBuilder().ConfigureServices(services =>
            {
                services.AddOpenTelemetryTracing(builder =>
                {
                    builder.AddInstrumentation(() =>
                    {
                        callbackRun = true;
                        return testInstrumentation;
                    });
                });
            });

            var host = builder.Build();

            Assert.False(callbackRun);
            Assert.False(testInstrumentation.Disposed);

            await host.StartAsync();

            Assert.True(callbackRun);
            Assert.False(testInstrumentation.Disposed);

            await host.StopAsync();

            Assert.True(callbackRun);
            Assert.False(testInstrumentation.Disposed);

            host.Dispose();

            Assert.True(callbackRun);
            Assert.True(testInstrumentation.Disposed);
        }

        [Fact]
        public void AddOpenTelemetryTracerProvider_HostBuilt_OpenTelemetrySdk_RegisteredAsSingleton()
        {
            var builder = new HostBuilder().ConfigureServices(services =>
            {
                services.AddOpenTelemetryTracing();
            });

            var host = builder.Build();

            var tracerProvider1 = host.Services.GetRequiredService<TracerProvider>();
            var tracerProvider2 = host.Services.GetRequiredService<TracerProvider>();

            Assert.Same(tracerProvider1, tracerProvider2);
        }

        [Fact]
        public void AddOpenTelemetryTracerProvider_ServiceProviderArgument_ServicesRegistered()
        {
            var testInstrumentation = new TestInstrumentation();

            var services = new ServiceCollection();
            services.AddSingleton(testInstrumentation);
            services.AddOpenTelemetryTracing(builder =>
            {
                builder.Configure(
                    (sp, b) => b.AddInstrumentation(() => sp.GetRequiredService<TestInstrumentation>()));
            });

            var serviceProvider = services.BuildServiceProvider();

            var tracerFactory = serviceProvider.GetRequiredService<TracerProvider>();
            Assert.NotNull(tracerFactory);

            Assert.False(testInstrumentation.Disposed);

            serviceProvider.Dispose();

            Assert.True(testInstrumentation.Disposed);
        }

        [Fact]
        public void AddOpenTelemetryTracerProvider_BadArgs_NullServiceCollection()
        {
            ServiceCollection services = null;
            Assert.Throws<ArgumentNullException>(() => services.AddOpenTelemetryTracing(null));
            Assert.Throws<ArgumentNullException>(() =>
                services.AddOpenTelemetryTracing(builder =>
                {
                    builder.Configure(
                        (sp, b) => b.AddInstrumentation(() => sp.GetRequiredService<TestInstrumentation>()));
                }));
        }

        [Fact]
        public void AddOpenTelemetryTracerProvider_GetServicesExtension()
        {
            var services = new ServiceCollection();
            services.AddOpenTelemetryTracing(builder => AddMyFeature(builder));

            using var serviceProvider = services.BuildServiceProvider();

            var tracerProvider = (TracerProviderSdk)serviceProvider.GetRequiredService<TracerProvider>();

            Assert.True(tracerProvider.Sampler is TestSampler);
        }

        [Fact]
        public void AddOpenTelemetryTracerProvider_NestedConfigureCallbacks()
        {
            int configureCalls = 0;
            var services = new ServiceCollection();
            services.AddOpenTelemetryTracing(builder => builder
                .Configure((sp1, builder1) =>
                {
                    configureCalls++;
                    builder1.Configure((sp2, builder2) =>
                    {
                        configureCalls++;
                    });
                }));

            using var serviceProvider = services.BuildServiceProvider();

            var tracerFactory = serviceProvider.GetRequiredService<TracerProvider>();

            Assert.Equal(2, configureCalls);
        }

        [Fact]
        public void AddOpenTelemetryTracerProvider_ConfigureCallbacksUsingExtensions()
        {
            var services = new ServiceCollection();

            services.AddSingleton<TestInstrumentation>();
            services.AddSingleton<TestProcessor>();
            services.AddSingleton<TestSampler>();

            services.AddOpenTelemetryTracing(builder => builder
                .Configure((sp1, builder1) =>
                {
                    builder1
                        .AddInstrumentation<TestInstrumentation>()
                        .AddProcessor<TestProcessor>()
                        .SetSampler<TestSampler>();
                }));

            using var serviceProvider = services.BuildServiceProvider();

            var tracerProvider = (TracerProviderSdk)serviceProvider.GetRequiredService<TracerProvider>();

            Assert.True(tracerProvider.Instrumentations.FirstOrDefault() is TestInstrumentation);
            Assert.True(tracerProvider.Processor is TestProcessor);
            Assert.True(tracerProvider.Sampler is TestSampler);
        }

        [Fact(Skip = "Known limitation. See issue 1215.")]
        public void AddOpenTelemetryTracerProvider_Idempotent()
        {
            var testInstrumentation1 = new TestInstrumentation();
            var testInstrumentation2 = new TestInstrumentation();

            var services = new ServiceCollection();
            services.AddSingleton(testInstrumentation1);
            services.AddOpenTelemetryTracing(builder =>
            {
                builder.AddInstrumentation(() => testInstrumentation1);
            });

            services.AddOpenTelemetryTracing(builder =>
            {
                builder.AddInstrumentation(() => testInstrumentation2);
            });

            var serviceProvider = services.BuildServiceProvider();

            var tracerFactory = serviceProvider.GetRequiredService<TracerProvider>();
            Assert.NotNull(tracerFactory);

            Assert.False(testInstrumentation1.Disposed);
            Assert.False(testInstrumentation2.Disposed);
            serviceProvider.Dispose();
            Assert.True(testInstrumentation1.Disposed);
            Assert.True(testInstrumentation2.Disposed);
        }

        private static TracerProviderBuilder AddMyFeature(TracerProviderBuilder tracerProviderBuilder)
        {
            (tracerProviderBuilder.GetServices() ?? throw new NotSupportedException("MyFeature requires a hosting TracerProviderBuilder instance."))
                .AddSingleton<TestSampler>();

            return tracerProviderBuilder.SetSampler<TestSampler>();
        }

        internal class TestInstrumentation : IDisposable
        {
            public bool Disposed { get; private set; }

            public void Dispose()
            {
                this.Disposed = true;
            }
        }

        internal class TestProcessor : BaseProcessor<Activity>
        {
        }

        internal class TestSampler : Sampler
        {
            public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
            {
                return new SamplingResult(SamplingDecision.RecordAndSample);
            }
        }
    }
}
