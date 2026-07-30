// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Android.TestApp;

// These tests run on the Android emulator (via Microsoft.Testing.Platform) and
// export real OTLP/HTTP to the in-process collector running on the CI host. The
// emulator reaches the host loopback through the special 10.0.2.2 alias.
[TestClass]
public sealed class AndroidEndToEndTests
{
    private static readonly Uri OtlpBaseAddress = new("http://10.0.2.2:4318");
    private static readonly TimeSpan FlushTimeout = TimeSpan.FromSeconds(10);

    [TestMethod]
    public void IsRunningOnAndroid()
        => Assert.IsTrue(OperatingSystem.IsAndroid(), "Expected the test to run on the Android runtime.");

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

        Assert.IsNotNull(meterProvider, "MeterProvider failed to build on Android.");

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

        Assert.IsNotNull(tracerProvider, "TracerProvider failed to build on Android.");

        using (var activity = instrumentation.ActivitySource.StartActivity(InstrumentationSource.ActivityName))
        {
            Assert.IsNotNull(activity, "ActivitySource produced no Activity - the SDK did not subscribe on Android.");
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
