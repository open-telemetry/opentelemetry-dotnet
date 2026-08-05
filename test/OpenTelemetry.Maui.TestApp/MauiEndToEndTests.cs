// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Maui.TestApp;

// These tests run inside the MAUI app on the Android emulator (via
// Microsoft.Testing.Platform) and export real OTLP/HTTP to the in-process
// collector running on the CI host. The emulator reaches the host loopback
// through the special 10.0.2.2 alias.
//
// Unlike the Android and Apple test apps, which build the providers themselves,
// these tests resolve them from the service provider MAUI's own startup created
// from MauiProgram, so the MAUI application host and the DI wiring in
// OpenTelemetry.Extensions.Hosting are covered as well as the SDK.
[TestClass]
public sealed class MauiEndToEndTests
{
    private static readonly TimeSpan FlushTimeout = TimeSpan.FromSeconds(10);

    private static IServiceProvider Services =>
        IPlatformApplication.Current?.Services
            ?? throw new InvalidOperationException("The MAUI application host was not initialized.");

    [TestMethod]
    public void IsRunningOnAndroid()
        => Assert.IsTrue(OperatingSystem.IsAndroid(), "Expected the test to run on the Android runtime.");

    [TestMethod]
    public void MauiApplicationHostIsInitialized()
    {
        Assert.IsNotNull(IPlatformApplication.Current, "MAUI did not initialize the platform application.");
        Assert.IsNotNull(IPlatformApplication.Current.Services, "The MAUI application host has no service provider.");
        Assert.IsInstanceOfType<App>(Application.Current, "MAUI did not create the application configured by MauiProgram.");
    }

    [TestMethod]
    public void LogsAreExported()
    {
        var loggerFactory = Services.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger(InstrumentationSource.LoggerName);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("{Message}", InstrumentationSource.LogBody);
        }
        else
        {
            Assert.Fail("The MAUI logging pipeline is not enabled for Information.");
        }
    }

    [TestMethod]
    public void MetricsAreExported()
    {
        var meterProvider = Services.GetRequiredService<MeterProvider>();
        var instrumentation = Services.GetRequiredService<InstrumentationSource>();

        instrumentation.Counter.Add(1);
        instrumentation.Histogram.Record(123.45);

        Assert.IsTrue(
            meterProvider.ForceFlush((int)FlushTimeout.TotalMilliseconds),
            "MeterProvider.ForceFlush() did not complete within the timeout.");
    }

    [TestMethod]
    public void TracesAreExported()
    {
        var tracerProvider = Services.GetRequiredService<TracerProvider>();
        var instrumentation = Services.GetRequiredService<InstrumentationSource>();

        using (var activity = instrumentation.ActivitySource.StartActivity(InstrumentationSource.ActivityName))
        {
            Assert.IsNotNull(activity, "ActivitySource produced no Activity - the SDK did not subscribe in the MAUI app.");
            activity.SetTag(InstrumentationSource.ActivityTagKey, InstrumentationSource.ActivityTagValue);
        }

        Assert.IsTrue(
            tracerProvider.ForceFlush((int)FlushTimeout.TotalMilliseconds),
            "TracerProvider.ForceFlush() did not complete within the timeout.");
    }
}
