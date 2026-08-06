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
    private static readonly Uri OtlpBaseAddress = new(InstrumentationSource.OtlpEndpoint);

    private static readonly TimeSpan FlushTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ExportTimeout = TimeSpan.FromSeconds(30);

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

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("{Message}", InstrumentationSource.LogBody);
        }

        Assert.IsTrue(
            loggerProvider.ForceFlush((int)FlushTimeout.TotalMilliseconds),
            $"Logs were not exported within {FlushTimeout}.");
    }

    [TestMethod]
    public void MetricsAreExported()
    {
        using var instrumentation = new InstrumentationSource();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(CreateResourceBuilder())
            .AddMeter(InstrumentationSource.MeterName)
            .AddOtlpExporter((options) => ConfigureOtlp(options, "v1/metrics"))
            .Build();

        Assert.IsNotNull(meterProvider, "MeterProvider failed to build on iOS.");

        instrumentation.Counter.Add(1);
        instrumentation.Histogram.Record(123.45);

        Assert.IsTrue(
            meterProvider.ForceFlush((int)FlushTimeout.TotalMilliseconds),
            $"Metrics were not exported within {FlushTimeout}.");
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

        using (var activity = instrumentation.ActivitySource.StartActivity(InstrumentationSource.ActivityName))
        {
            Assert.IsNotNull(activity, "ActivitySource produced no Activity - the SDK did not subscribe on iOS.");
            activity.SetTag(InstrumentationSource.ActivityTagKey, InstrumentationSource.ActivityTagValue);
        }

        Assert.IsTrue(
            tracerProvider.ForceFlush((int)FlushTimeout.TotalMilliseconds),
            $"Traces were not exported within {FlushTimeout}.");
    }

    private static ResourceBuilder CreateResourceBuilder()
        => ResourceBuilder.CreateDefault().AddService(InstrumentationSource.ServiceName);

    private static void ConfigureOtlp(OtlpExporterOptions options, string signalPath)
    {
        options.Protocol = OtlpExportProtocol.HttpProtobuf;
        options.Endpoint = new(OtlpBaseAddress, signalPath);
        options.TimeoutMilliseconds = (int)ExportTimeout.TotalMilliseconds;
    }
}
