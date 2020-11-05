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
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Extensions.Hosting
{
    public class HostingExtensionsTests
    {
        private static readonly string TestActivitySourceName = "TestActivitySource";
        private static readonly ActivitySource TestActivitySource = new ActivitySource(TestActivitySourceName);

        [Fact]
        public async Task AddOpenTelemetryTracingInstrumentationCreationAndDisposal()
        {
            var testInstrumentation = new TestInstrumentation();
            var callbackRun = false;

            var builder = new HostBuilder().ConfigureServices(services =>
            {
                services.AddOpenTelemetryTracing();
                services.Configure<TracerProviderBuilder>((builder) =>
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
        public void AddOpenTelemetryTracing_HostBuilt_OpenTelemetrySdk_RegisteredAsSingleton()
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
        public async Task AddOpenTelemetryTracingDetectMultipleCalls()
        {
            var testInstrumentation = new TestInstrumentation();
            int instrumentationCallBackCount = 0;

            var builder = new HostBuilder().ConfigureServices(services =>
            {
                services.AddOpenTelemetryTracing();
                services.AddOpenTelemetryTracing();
                services.AddOpenTelemetryTracing();
                services.AddOpenTelemetryTracing();
                services.Configure<TracerProviderBuilder>((builder) =>
                {
                    builder.AddInstrumentation(() =>
                    {
                        instrumentationCallBackCount++;
                        return testInstrumentation;
                    });
                });
            });

            var host = builder.Build();
            await host.StartAsync();
            Assert.Equal(1, instrumentationCallBackCount);
            await host.StopAsync();
            host.Dispose();
        }

        [Fact]
        public async Task AddOpenTelemetryTracingE2EDemo()
        {
            var testProcessor = new TestActivityProcessor();
            var resource = Resources.Resources.CreateServiceResource("serviceName");
            testProcessor.StartAction = (act) => Assert.Equal("MyActivityName", act.DisplayName);
            testProcessor.EndAction = (act) => Assert.Equal(resource, act.GetResource());
            var builder = new HostBuilder().ConfigureServices(services =>
            {
                // Add OpentelemetryTracing.
                services.AddOpenTelemetryTracing();
                services.AddOpenTelemetryTracing();
                services.AddOpenTelemetryTracing();

                // Configure TracerProviderBuilder
                services.Configure<TracerProviderBuilder>((builder) => builder.SetSampler(new AlwaysOnSampler()));

                // Keep configuring TracerProviderBuilder
                services.Configure<TracerProviderBuilder>((builder) => builder.AddProcessor(testProcessor));

                // Keep configuring TracerProviderBuilder
                services.Configure<TracerProviderBuilder>((builder) => builder.SetResource(resource));

                // Keep configuring TracerProviderBuilder
                services.Configure<TracerProviderBuilder>((builder) => builder.AddSource(TestActivitySourceName));
            });

            var host = builder.Build();

            // Host not started, so this activity is not listened
            var myActivity = TestActivitySource.StartActivity("MyActivityName");
            Assert.Null(myActivity);

            await host.StartAsync();

            myActivity = TestActivitySource.StartActivity("MyActivityName");
            Assert.NotNull(myActivity);
            myActivity.Stop();

            await host.StopAsync();
        }

        internal class TestInstrumentation : IDisposable
        {
            public bool Disposed { get; private set; }

            public void Dispose()
            {
                this.Disposed = true;
            }
        }

        internal class TestActivityProcessor : BaseProcessor<Activity>
        {
            public Action<Activity> StartAction;
            public Action<Activity> EndAction;

            public TestActivityProcessor()
            {
            }

            public TestActivityProcessor(Action<Activity> onStart, Action<Activity> onEnd)
            {
                this.StartAction = onStart;
                this.EndAction = onEnd;
            }

            public bool ShutdownCalled { get; private set; } = false;

            public bool ForceFlushCalled { get; private set; } = false;

            public bool DisposedCalled { get; private set; } = false;

            public override void OnStart(Activity span)
            {
                this.StartAction?.Invoke(span);
            }

            public override void OnEnd(Activity span)
            {
                this.EndAction?.Invoke(span);
            }

            protected override bool OnForceFlush(int timeoutMilliseconds)
            {
                this.ForceFlushCalled = true;
                return true;
            }

            protected override bool OnShutdown(int timeoutMilliseconds)
            {
                this.ShutdownCalled = true;
                return true;
            }

            protected override void Dispose(bool disposing)
            {
                this.DisposedCalled = true;
            }
        }
    }
}
