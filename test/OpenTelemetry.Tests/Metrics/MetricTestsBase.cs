// <copyright file="MetricTestsBase.cs" company="OpenTelemetry Authors">
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

#if BUILDING_HOSTING_TESTS
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
#endif
using Xunit;

namespace OpenTelemetry.Metrics.Tests;

public class MetricTestsBase
{
    public const string EmitOverFlowAttributeConfigKey = "OTEL_DOTNET_EXPERIMENTAL_METRICS_EMIT_OVERFLOW_ATTRIBUTE";

    public static IDisposable BuildMeterProvider(
        out MeterProvider meterProvider,
        Action<MeterProviderBuilder> configure)
    {
        if (configure == null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

#if BUILDING_HOSTING_TESTS
        var host = BuildHost(
            useWithMetricsStyle: false,
            configureMeterProviderBuilder: configure);

        meterProvider = host.Services.GetService<MeterProvider>();

        return host;
#else
        var builder = Sdk.CreateMeterProviderBuilder();

        configure(builder);

        return meterProvider = builder.Build();
#endif
    }

#if BUILDING_HOSTING_TESTS
    public static IHost BuildHost(
        bool useWithMetricsStyle,
        Action<HostBuilderContext, IConfigurationBuilder> configureAppConfiguration = null,
        Action<IServiceCollection> configureServices = null,
        Action<IMetricsBuilder> configureMetricsBuilder = null,
        Action<HostingMeterProviderBuilder> configureMeterProviderBuilder = null)
    {
        var hostBuilder = new HostBuilder()
            .ConfigureDefaults(null)
            .ConfigureAppConfiguration((context, builder) =>
            {
                configureAppConfiguration?.Invoke(context, builder);
            })
            .ConfigureServices(services =>
            {
                configureServices?.Invoke(services);

                services.AddMetrics(builder =>
                {
                    configureMetricsBuilder?.Invoke(builder);

                    if (!useWithMetricsStyle)
                    {
                        builder.UseOpenTelemetry(metricsBuilder => ConfigureBuilder(metricsBuilder, configureMeterProviderBuilder));
                    }
                });

                if (useWithMetricsStyle)
                {
                    services
                        .AddOpenTelemetry()
                        .WithMetrics(metricsBuilder => ConfigureBuilder(metricsBuilder, configureMeterProviderBuilder));
                }

                services.AddHostedService<MetricsSubscriptionManagerCleanupHostedService>();
            });

        var host = hostBuilder.Build();

        host.Start();

        return host;

        static void ConfigureBuilder(MeterProviderBuilder builder, Action<HostingMeterProviderBuilder> configureMeterProviderBuilder)
        {
            IServiceCollection localServices = null;

            builder.ConfigureServices(services => localServices = services);

            Debug.Assert(localServices != null, "localServices was null");

            var testBuilder = new HostingMeterProviderBuilder(localServices);
            configureMeterProviderBuilder?.Invoke(testBuilder);
        }
    }
#endif

    // This method relies on the assumption that MetricPoints are exported in the order in which they are emitted.
    // For Delta AggregationTemporality, this holds true only until the AggregatorStore has not begun recaliming the MetricPoints.
    public static void ValidateMetricPointTags(List<KeyValuePair<string, object>> expectedTags, ReadOnlyTagCollection actualTags)
    {
        int tagIndex = 0;
        foreach (var tag in actualTags)
        {
            Assert.Equal(expectedTags[tagIndex].Key, tag.Key);
            Assert.Equal(expectedTags[tagIndex].Value, tag.Value);
            tagIndex++;
        }

        Assert.Equal(expectedTags.Count, tagIndex);
    }

    public static long GetLongSum(List<Metric> metrics)
    {
        long sum = 0;
        foreach (var metric in metrics)
        {
            foreach (ref readonly var metricPoint in metric.GetMetricPoints())
            {
                if (metric.MetricType.IsSum())
                {
                    sum += metricPoint.GetSumLong();
                }
                else
                {
                    sum += metricPoint.GetGaugeLastValueLong();
                }
            }
        }

        return sum;
    }

    public static double GetDoubleSum(List<Metric> metrics)
    {
        double sum = 0;
        foreach (var metric in metrics)
        {
            foreach (ref readonly var metricPoint in metric.GetMetricPoints())
            {
                if (metric.MetricType.IsSum())
                {
                    sum += metricPoint.GetSumDouble();
                }
                else
                {
                    sum += metricPoint.GetGaugeLastValueDouble();
                }
            }
        }

        return sum;
    }

    public static int GetNumberOfMetricPoints(List<Metric> metrics)
    {
        int count = 0;
        foreach (var metric in metrics)
        {
            foreach (ref readonly var metricPoint in metric.GetMetricPoints())
            {
                count++;
            }
        }

        return count;
    }

    public static MetricPoint? GetFirstMetricPoint(List<Metric> metrics)
    {
        foreach (var metric in metrics)
        {
            foreach (ref readonly var metricPoint in metric.GetMetricPoints())
            {
                return metricPoint;
            }
        }

        return null;
    }

    // This method relies on the assumption that MetricPoints are exported in the order in which they are emitted.
    // For Delta AggregationTemporality, this holds true only until the AggregatorStore has not begun recaliming the MetricPoints.
    // Provide tags input sorted by Key
    public static void CheckTagsForNthMetricPoint(List<Metric> metrics, List<KeyValuePair<string, object>> tags, int n)
    {
        var metric = metrics[0];
        var metricPointEnumerator = metric.GetMetricPoints().GetEnumerator();

        for (int i = 0; i < n; i++)
        {
            Assert.True(metricPointEnumerator.MoveNext());
        }

        int index = 0;
        var metricPoint = metricPointEnumerator.Current;
        foreach (var tag in metricPoint.Tags)
        {
            Assert.Equal(tags[index].Key, tag.Key);
            Assert.Equal(tags[index].Value, tag.Value);
            index++;
        }
    }

    internal static Exemplar[] GetExemplars(MetricPoint mp)
    {
        return mp.GetExemplars().Where(exemplar => exemplar.Timestamp != default).ToArray();
    }

#if BUILDING_HOSTING_TESTS
    public sealed class HostingMeterProviderBuilder : MeterProviderBuilderBase
    {
        public HostingMeterProviderBuilder(IServiceCollection services)
            : base(services)
        {
        }

        public override MeterProviderBuilder AddMeter(params string[] names)
        {
            return this.ConfigureServices(services =>
            {
                foreach (var name in names)
                {
                    // Note: The entire purpose of this class is to use the
                    // IMetricsBuilder API to enable Metrics and NOT the
                    // traditional AddMeter API.
                    services.AddMetrics(builder => builder.EnableMetrics(name));
                }
            });
        }

        public MeterProviderBuilder AddSdkMeter(params string[] names)
        {
            return base.AddMeter(names);
        }
    }

    private sealed class MetricsSubscriptionManagerCleanupHostedService : IHostedService, IDisposable
    {
        private readonly object metricsSubscriptionManager;

        public MetricsSubscriptionManagerCleanupHostedService(IServiceProvider serviceProvider)
        {
            this.metricsSubscriptionManager = serviceProvider.GetService(
                typeof(ConsoleMetrics).Assembly.GetType("Microsoft.Extensions.Diagnostics.Metrics.MetricsSubscriptionManager"));

            if (this.metricsSubscriptionManager == null)
            {
                throw new InvalidOperationException("MetricsSubscriptionManager could not be found reflectively.");
            }
        }

        public void Dispose()
        {
            // Note: The current version of MetricsSubscriptionManager seems to
            // be bugged in that it doesn't implement IDisposable. This hack
            // manually invokes Dispose so that tests don't clobber each other.
            // See: https://github.com/dotnet/runtime/issues/94434.
            this.metricsSubscriptionManager.GetType().GetMethod("Dispose").Invoke(this.metricsSubscriptionManager, null);
        }

        public Task StartAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
#endif
}
