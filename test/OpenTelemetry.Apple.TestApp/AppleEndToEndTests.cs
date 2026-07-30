// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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

    private static readonly TimeSpan FlushTimeout = TimeSpan.FromSeconds(10);

    [TestMethod]
    public void IsRunningOnApplePlatform()
        => Assert.IsTrue(OperatingSystem.IsIOS(), "Expected the test to run on the iOS runtime.");

    [TestMethod]
    public void LogsAreExported()
    {
        using var loggerFactory = LoggerFactory.Create((builder) =>
        {
            builder.AddOpenTelemetry((options) =>
            {
                options.SetResourceBuilder(CreateResourceBuilder());
                options.IncludeFormattedMessage = true;
                options.AddOtlpExporter((exporterOptions) => ConfigureOtlp(exporterOptions, "v1/logs"));
            });
        });

        var logger = loggerFactory.CreateLogger(InstrumentationSource.LoggerName);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("{Message}", InstrumentationSource.LogBody);
        }
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

        meterProvider.ForceFlush((int)FlushTimeout.TotalMilliseconds);
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

        tracerProvider.ForceFlush((int)FlushTimeout.TotalMilliseconds);
    }

    private static ResourceBuilder CreateResourceBuilder()
        => ResourceBuilder.CreateDefault().AddService(InstrumentationSource.ServiceName);

    private static void ConfigureOtlp(OtlpExporterOptions options, string signalPath)
    {
        options.Protocol = OtlpExportProtocol.HttpProtobuf;
        options.Endpoint = new(OtlpBaseAddress, signalPath);
    }
}
