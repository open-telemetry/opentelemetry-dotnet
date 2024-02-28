// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if BUILDING_HOSTING_TESTS
using System.Diagnostics;
#endif
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
#if BUILDING_HOSTING_TESTS
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
#endif
using Xunit;

namespace OpenTelemetry.Metrics.Tests;

public class MetricTestsBase
{
    public const string EmitOverFlowAttributeConfigKey = "OTEL_DOTNET_EXPERIMENTAL_METRICS_EMIT_OVERFLOW_ATTRIBUTE";
    public const string ReclaimUnusedMetricPointsConfigKey = "OTEL_DOTNET_EXPERIMENTAL_METRICS_RECLAIM_UNUSED_METRIC_POINTS";

    protected readonly IConfiguration configuration;

    protected MetricTestsBase()
    {
    }

    protected MetricTestsBase(IConfiguration configuration)
    {
        this.configuration = configuration;
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

    public IDisposable BuildMeterProvider(
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
            configureMeterProviderBuilder: configure,
            configureServices: services =>
            {
                if (this.configuration != null)
                {
                    services.AddSingleton(this.configuration);
                }
            });

        meterProvider = host.Services.GetService<MeterProvider>();

        return host;
#else
        var builder = Sdk.CreateMeterProviderBuilder();

        if (this.configuration != null)
        {
            builder.ConfigureServices(services => services.AddSingleton(this.configuration));
        }

        configure(builder);

        return meterProvider = builder.Build();
#endif
    }

    internal static IReadOnlyList<Exemplar> GetExemplars(MetricPoint mp)
    {
        if (mp.TryGetExemplars(out var exemplars))
        {
            return exemplars.Value.ToReadOnlyList();
        }

        return Array.Empty<Exemplar>();
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
