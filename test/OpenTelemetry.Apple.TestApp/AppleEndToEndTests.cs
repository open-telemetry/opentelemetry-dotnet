// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Apple.TestApp;

// These tests run on the iOS simulator (via Microsoft.Testing.Platform) and
// export real OTLP/HTTP to the in-process collector running on the host.
[TestClass]
public sealed class AppleEndToEndTests
{
    private const int FlushAttempts = 3;

    private static readonly Uri OtlpBaseAddress = new(InstrumentationSource.OtlpEndpoint);

    private static readonly TimeSpan FlushTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(30);

    [TestMethod]
    public void IsRunningOnApplePlatform()
        => Assert.IsTrue(OperatingSystem.IsIOS(), "Expected the test to run on the iOS runtime.");

    [TestMethod]
    public void LogsAreExported()
    {
        // Access logs through LoggerProvider to allow for explicit flushing
        var services = new ServiceCollection();

        services.AddLogging((builder) =>
        {
            builder.UseOpenTelemetry(
                (loggerProviderBuilder) => loggerProviderBuilder
                    .SetResourceBuilder(CreateResourceBuilder())
                    .AddOtlpExporter((exporterOptions) => ConfigureOtlp(exporterOptions, "v1/logs")),
                (options) => options.IncludeFormattedMessage = true);
        });

        using var serviceProvider = services.BuildServiceProvider();

        var loggerProvider = serviceProvider.GetRequiredService<LoggerProvider>();
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(InstrumentationSource.LoggerName);

        Assert.IsTrue(logger.IsEnabled(LogLevel.Information), "Information logs are not enabled.");

        RecordAndShutdown(
            () =>
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("{Message}", InstrumentationSource.LogBody);
                }
            },
            loggerProvider.ForceFlush,
            loggerProvider.Shutdown);
    }

    [TestMethod]
    public void MetricsAreExported()
    {
        using var instrumentation = new InstrumentationSource();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(CreateResourceBuilder())
            .AddMeter(InstrumentationSource.MeterName)
            .AddOtlpExporter((exporterOptions, readerOptions) =>
            {
                ConfigureOtlp(exporterOptions, "v1/metrics");
                readerOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = Timeout.Infinite;
            })
            .Build();

        Assert.IsNotNull(meterProvider, "MeterProvider failed to build on iOS.");

        instrumentation.Counter.Add(1);
        instrumentation.Histogram.Record(123.45);

        FlushAndShutdown("Metrics", meterProvider.ForceFlush, meterProvider.Shutdown);
    }

    [TestMethod]
    public void TracesAreExported()
    {
        using var instrumentation = new InstrumentationSource();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(CreateResourceBuilder())
            .AddSource(InstrumentationSource.ActivitySourceName)
            .SetSampler(new AlwaysOnSampler())
            .AddOtlpExporter((options) => ConfigureOtlp(options, "v1/traces"))
            .Build();

        Assert.IsNotNull(tracerProvider, "TracerProvider failed to build on iOS.");

        RecordAndShutdown(
            () =>
            {
                using var activity = instrumentation.ActivitySource.StartActivity(InstrumentationSource.ActivityName);

                Assert.IsNotNull(activity, "ActivitySource produced no Activity - the SDK did not subscribe on iOS.");
                activity.SetTag(InstrumentationSource.ActivityTagKey, InstrumentationSource.ActivityTagValue);
            },
            tracerProvider.ForceFlush,
            tracerProvider.Shutdown);
    }

    private static ResourceBuilder CreateResourceBuilder()
        => ResourceBuilder.CreateDefault().AddService(InstrumentationSource.ServiceName);

    private static void RecordAndShutdown(Action record, Func<int, bool> forceFlush, Func<int, bool> shutdown)
    {
        var flushed = false;
        var deadline = DateTime.UtcNow + FlushTimeout;

        for (var attempt = 0; attempt < FlushAttempts; attempt++)
        {
            var remaining = deadline - DateTime.UtcNow;

            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            record();

            // One attempt reaching the exporter is sufficient for the test to pass
            flushed |= forceFlush((int)remaining.TotalMilliseconds);
        }

        Assert.IsTrue(flushed, $"The telemetry was not flushed within {FlushTimeout}.");

        _ = shutdown((int)ShutdownTimeout.TotalMilliseconds);
    }

    /// <summary>
    /// Flushes the telemetry recorded by a test and then shuts its provider down.
    /// </summary>
    /// <param name="signal">The name of the signal being flushed, used in the assertion message.</param>
    /// <param name="forceFlush">The <c>ForceFlush</c> method of the provider to flush.</param>
    /// <param name="shutdown">The <c>Shutdown</c> method of the provider to shut down.</param>
    private static void FlushAndShutdown(string signal, Func<int, bool> forceFlush, Func<int, bool> shutdown)
    {
        Assert.IsTrue(TryFlush(forceFlush), $"{signal} were not exported within {FlushTimeout}.");
        _ = shutdown((int)ShutdownTimeout.TotalMilliseconds);
    }

    /// <summary>
    /// Flushes the telemetry recorded by a test, retrying within
    /// <see cref="FlushTimeout"/> if an export does not get through.
    /// </summary>
    /// <param name="forceFlush">The <c>ForceFlush</c> method of the provider to flush.</param>
    /// <returns><see langword="true"/> if the telemetry was flushed; otherwise <see langword="false"/>.</returns>
    private static bool TryFlush(Func<int, bool> forceFlush)
    {
        var deadline = DateTime.UtcNow + FlushTimeout;

        for (var attempt = 0; attempt < FlushAttempts; attempt++)
        {
            var remaining = deadline - DateTime.UtcNow;

            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            if (forceFlush((int)remaining.TotalMilliseconds))
            {
                return true;
            }
        }

        return false;
    }

    private static void ConfigureOtlp(OtlpExporterOptions options, string signalPath)
    {
        options.Protocol = OtlpExportProtocol.HttpProtobuf;
        options.Endpoint = new(OtlpBaseAddress, signalPath);

        // Export over the connection the entry point has already opened to the
        // host rather than over one of this exporter's own
        options.HttpClientFactory = static () => TestRunner.OtlpHttpClient;
        options.TimeoutMilliseconds = (int)TestRunner.ExportTimeout.TotalMilliseconds;
    }
}
