// <copyright file="TracerProviderBuilderExtensionsTest.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Trace.Tests
{
    public class TracerProviderBuilderExtensionsTest
    {
        private const string ActivitySourceName = "TracerProviderBuilderExtensionsTest";

        [Fact]
        public void SetErrorStatusOnExceptionEnabled()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource(ActivitySourceName)
                .SetSampler(new AlwaysOnSampler())
                .SetErrorStatusOnException(false)
                .SetErrorStatusOnException(false)
                .SetErrorStatusOnException(true)
                .SetErrorStatusOnException(true)
                .SetErrorStatusOnException(false)
                .SetErrorStatusOnException()
                .Build();

            Activity activity = null;

            try
            {
                using (activity = activitySource.StartActivity("Activity"))
                {
                    throw new Exception("Oops!");
                }
            }
            catch (Exception)
            {
            }

            Assert.Equal(StatusCode.Error, activity.GetStatus().StatusCode);
        }

        [Fact]
        public void SetErrorStatusOnExceptionDisabled()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource(ActivitySourceName)
                .SetSampler(new AlwaysOnSampler())
                .SetErrorStatusOnException()
                .SetErrorStatusOnException(false)
                .Build();

            Activity activity = null;

            try
            {
                using (activity = activitySource.StartActivity("Activity"))
                {
                    throw new Exception("Oops!");
                }
            }
            catch (Exception)
            {
            }

            Assert.Equal(StatusCode.Unset, activity.GetStatus().StatusCode);
        }

        [Fact]
        public void SetErrorStatusOnExceptionDefault()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource(ActivitySourceName)
                .SetSampler(new AlwaysOnSampler())
                .Build();

            Activity activity = null;

            try
            {
                using (activity = activitySource.StartActivity("Activity"))
                {
                    throw new Exception("Oops!");
                }
            }
            catch (Exception)
            {
            }

            Assert.Equal(StatusCode.Unset, activity.GetStatus().StatusCode);
        }

        [Fact]
        public void ServiceLifecycleAvailableToSDKBuilderTest()
        {
            var builder = Sdk.CreateTracerProviderBuilder();

            builder.ConfigureServices(services => services.AddSingleton<MyInstrumentation>());

            MyInstrumentation myInstrumentation = null;

            RunBuilderServiceLifecycleTest(
                builder,
                () =>
                {
                    var provider = builder.Build() as TracerProviderSdk;

                    // Note: Build can only be called once
                    Assert.Throws<NotSupportedException>(() => builder.Build());

                    Assert.NotNull(provider);
                    Assert.NotNull(provider.OwnedServiceProvider);

                    myInstrumentation = ((IServiceProvider)provider.OwnedServiceProvider).GetRequiredService<MyInstrumentation>();

                    return provider;
                },
                provider =>
                {
                    provider.Dispose();
                });

            Assert.NotNull(myInstrumentation);
            Assert.True(myInstrumentation.Disposed);
        }

        [Fact]
        public void ServiceLifecycleAvailableToServicesBuilderTest()
        {
            var services = new ServiceCollection();

            bool testRun = false;

            ServiceProvider serviceProvider = null;
            TracerProviderSdk provider = null;

            services.ConfigureOpenTelemetryTracing(builder =>
            {
                testRun = true;

                RunBuilderServiceLifecycleTest(
                    builder,
                    () =>
                    {
                        // Note: Build can't be called directly on builder tied to external services
                        Assert.Throws<NotSupportedException>(() => builder.Build());

                        serviceProvider = services.BuildServiceProvider();

                        provider = serviceProvider.GetRequiredService<TracerProvider>() as TracerProviderSdk;

                        Assert.NotNull(provider);
                        Assert.Null(provider.OwnedServiceProvider);

                        return provider;
                    },
                    (provider) => { });
            });

            Assert.True(testRun);

            Assert.NotNull(serviceProvider);
            Assert.NotNull(provider);

            Assert.False(provider.Disposed);

            serviceProvider.Dispose();

            Assert.True(provider.Disposed);
        }

        [Fact]
        public void SingleProviderForServiceCollectionTest()
        {
            var services = new ServiceCollection();

            services.ConfigureOpenTelemetryTracing(builder =>
            {
                builder.AddInstrumentation<MyInstrumentation>(() => new());
            });

            services.ConfigureOpenTelemetryTracing(builder =>
            {
                builder.AddInstrumentation<MyInstrumentation>(() => new());
            });

            using var serviceProvider = services.BuildServiceProvider();

            Assert.NotNull(serviceProvider);

            var tracerProviders = serviceProvider.GetServices<TracerProvider>();

            Assert.Single(tracerProviders);

            var provider = tracerProviders.First() as TracerProviderSdk;

            Assert.NotNull(provider);

            Assert.Equal(2, provider.Instrumentations.Count);
        }

        [Fact]
        public void AddProcessorUsingDependencyInjectionTest()
        {
            var builder = Sdk.CreateTracerProviderBuilder();

            builder.AddProcessor<MyProcessor>();
            builder.AddProcessor<MyProcessor>();

            using var provider = builder.Build() as TracerProviderSdk;

            Assert.NotNull(provider);

            var processors = ((IServiceProvider)provider.OwnedServiceProvider).GetServices<MyProcessor>();

            // Note: Two "Add" calls but it is a singleton so only a single registration is produced
            Assert.Single(processors);

            var processor = provider.Processor as CompositeProcessor<Activity>;

            Assert.NotNull(processor);

            // Note: Two "Add" calls due yield two processors added to provider, even though they are the same
            Assert.True(processor.Head.Value is MyProcessor);
            Assert.True(processor.Head.Next?.Value is MyProcessor);
        }

        [Fact]
        public void SetAndConfigureResourceTest()
        {
            var builder = Sdk.CreateTracerProviderBuilder();

            int configureInvocations = 0;

            builder.SetResourceBuilder(ResourceBuilder.CreateEmpty().AddService("Test"));
            builder.ConfigureResource(builder =>
            {
                configureInvocations++;

                Assert.Single(builder.Resources);

                builder.AddAttributes(new Dictionary<string, object>() { ["key1"] = "value1" });

                Assert.Equal(2, builder.Resources.Count);
            });
            builder.SetResourceBuilder(ResourceBuilder.CreateEmpty());
            builder.ConfigureResource(builder =>
            {
                configureInvocations++;

                Assert.Empty(builder.Resources);

                builder.AddAttributes(new Dictionary<string, object>() { ["key2"] = "value2" });

                Assert.Single(builder.Resources);
            });

            using var provider = builder.Build() as TracerProviderSdk;

            Assert.Equal(2, configureInvocations);

            Assert.Single(provider.Resource.Attributes);
            Assert.Contains(provider.Resource.Attributes, kvp => kvp.Key == "key2" && (string)kvp.Value == "value2");
        }

        [Fact]
        public void AddExporterTest()
        {
            var builder = Sdk.CreateTracerProviderBuilder();

            builder.AddExporter(ExportProcessorType.Simple, new MyExporter());
            builder.AddExporter<MyExporter>(ExportProcessorType.Batch);

            using var provider = builder.Build() as TracerProviderSdk;

            Assert.NotNull(provider);

            var processor = provider.Processor as CompositeProcessor<Activity>;

            Assert.NotNull(processor);

            var firstProcessor = processor.Head.Value;
            var secondProcessor = processor.Head.Next?.Value;

            Assert.True(firstProcessor is SimpleActivityExportProcessor simpleProcessor && simpleProcessor.Exporter is MyExporter);
            Assert.True(secondProcessor is BatchActivityExportProcessor batchProcessor && batchProcessor.Exporter is MyExporter);
        }

        [Fact]
        public void AddExporterWithOptionsTest()
        {
            int optionsInvocations = 0;

            var builder = Sdk.CreateTracerProviderBuilder();

            builder.ConfigureServices(services =>
            {
                services.Configure<ExportActivityProcessorOptions>(options =>
                {
                    // Note: This is testing options integration

                    optionsInvocations++;

                    options.BatchExportProcessorOptions.MaxExportBatchSize = 18;
                });
            });

            builder.AddExporter(
                ExportProcessorType.Simple,
                new MyExporter(),
                options =>
                {
                    // Note: Options delegate isn't invoked for simple processor type
                    Assert.True(false);
                });
            builder.AddExporter<MyExporter>(
                ExportProcessorType.Batch,
                options =>
                {
                    optionsInvocations++;

                    Assert.Equal(18, options.BatchExportProcessorOptions.MaxExportBatchSize);

                    options.BatchExportProcessorOptions.MaxExportBatchSize = 100;
                });

            using var provider = builder.Build() as TracerProviderSdk;

            Assert.NotNull(provider);

            Assert.Equal(2, optionsInvocations);

            var processor = provider.Processor as CompositeProcessor<Activity>;

            Assert.NotNull(processor);

            var firstProcessor = processor.Head.Value;
            var secondProcessor = processor.Head.Next?.Value;

            Assert.True(firstProcessor is SimpleActivityExportProcessor simpleProcessor && simpleProcessor.Exporter is MyExporter);
            Assert.True(secondProcessor is BatchActivityExportProcessor batchProcessor
                && batchProcessor.Exporter is MyExporter
                && batchProcessor.MaxExportBatchSize == 100);
        }

        [Fact]
        public void AddExporterNamedOptionsTest()
        {
            var builder = Sdk.CreateTracerProviderBuilder();

            int defaultOptionsConfigureInvocations = 0;
            int namedOptionsConfigureInvocations = 0;

            builder.ConfigureServices(services =>
            {
                services.Configure<ExportActivityProcessorOptions>(o => defaultOptionsConfigureInvocations++);

                services.Configure<ExportActivityProcessorOptions>("Exporter2", o => namedOptionsConfigureInvocations++);
            });

            builder.AddExporter(ExportProcessorType.Batch, new MyExporter());
            builder.AddExporter(ExportProcessorType.Batch, new MyExporter(), name: "Exporter2", configure: null);
            builder.AddExporter<MyExporter>(ExportProcessorType.Batch);
            builder.AddExporter<MyExporter>(ExportProcessorType.Batch, name: "Exporter2", configure: null);

            using var provider = builder.Build() as TracerProviderSdk;

            Assert.NotNull(provider);

            Assert.Equal(1, defaultOptionsConfigureInvocations);
            Assert.Equal(1, namedOptionsConfigureInvocations);
        }

        private static void RunBuilderServiceLifecycleTest(
            TracerProviderBuilder builder,
            Func<TracerProviderSdk> buildFunc,
            Action<TracerProviderSdk> postAction)
        {
            var baseBuilder = builder as TracerProviderBuilderBase;
            Assert.Null(baseBuilder.State);

            builder
                .AddSource("TestSource")
                .AddLegacySource("TestLegacySource")
                .SetSampler<MySampler>();

            bool configureServicesCalled = false;
            builder.ConfigureServices(services =>
            {
                configureServicesCalled = true;

                Assert.NotNull(services);

                services.TryAddSingleton<MyProcessor>();

                services.ConfigureOpenTelemetryTracing(b =>
                {
                    // Note: This is strange to call ConfigureOpenTelemetryTracing here, but supported
                    b.AddInstrumentation<MyInstrumentation>();
                });
            });

            int configureBuilderInvocations = 0;
            builder.ConfigureBuilder((sp, builder) =>
            {
                configureBuilderInvocations++;

                var baseBuilder = builder as TracerProviderBuilderBase;
                Assert.NotNull(baseBuilder?.State);

                builder
                    .AddSource("TestSource2")
                    .AddLegacySource("TestLegacySource2");

                Assert.Contains(baseBuilder.State.Sources, s => s == "TestSource");
                Assert.Contains(baseBuilder.State.Sources, s => s == "TestSource2");
                Assert.Contains(baseBuilder.State.LegacyActivityOperationNames, s => s == "TestLegacySource");
                Assert.Contains(baseBuilder.State.LegacyActivityOperationNames, s => s == "TestLegacySource2");

                // Note: Services can't be configured at this stage
                Assert.Throws<NotSupportedException>(
                    () => builder.ConfigureServices(services => services.TryAddSingleton<TracerProviderBuilderExtensionsTest>()));

                builder.AddProcessor(sp.GetRequiredService<MyProcessor>());

                builder.ConfigureBuilder((_, b) =>
                {
                    // Note: ConfigureBuilder calls can be nested, this is supported
                    configureBuilderInvocations++;

                    b.ConfigureBuilder((_, _) =>
                    {
                        configureBuilderInvocations++;
                    });
                });
            });

            var provider = buildFunc();

            Assert.True(configureServicesCalled);
            Assert.Equal(3, configureBuilderInvocations);

            Assert.True(provider.Sampler is MySampler);
            Assert.Single(provider.Instrumentations);
            Assert.True(provider.Instrumentations[0] is MyInstrumentation);
            Assert.True(provider.Processor is MyProcessor);

            postAction(provider);
        }

        private sealed class MySampler : Sampler
        {
            public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
            {
                return new SamplingResult(SamplingDecision.RecordAndSample);
            }
        }

        private sealed class MyInstrumentation : IDisposable
        {
            internal bool Disposed;

            public void Dispose()
            {
                this.Disposed = true;
            }
        }

        private sealed class MyProcessor : BaseProcessor<Activity>
        {
        }

        private sealed class MyExporter : BaseExporter<Activity>
        {
            public override ExportResult Export(in Batch<Activity> batch)
            {
                return ExportResult.Success;
            }
        }
    }
}
