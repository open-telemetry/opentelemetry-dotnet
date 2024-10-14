// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Tests;

public class OpenTelemetrySdkTests
{
    [Fact]
    public void BuilderDelegateRequiredTest()
    {
        Assert.Throws<ArgumentNullException>(() => OpenTelemetrySdk.Create(null!));
    }

    [Fact]
    public void NoopProvidersReturnedTest()
    {
        bool builderDelegateInvoked = false;

        using var sdk = OpenTelemetrySdk.Create(builder =>
        {
            builderDelegateInvoked = true;
            Assert.NotNull(builder.Services);
        });

        Assert.True(builderDelegateInvoked);

        Assert.NotNull(sdk);
        Assert.NotNull(sdk.Services);
        Assert.True(sdk.LoggerProvider is OpenTelemetrySdk.NoopLoggerProvider);
        Assert.True(sdk.MeterProvider is OpenTelemetrySdk.NoopMeterProvider);
        Assert.True(sdk.TracerProvider is OpenTelemetrySdk.NoopTracerProvider);
        Assert.True(sdk.GetLoggerFactory() is NullLoggerFactory);
    }

    [Fact]
    public void ProvidersCreatedAndDisposedTest()
    {
        var sdk = OpenTelemetrySdk.Create(builder =>
        {
            builder
                .WithLogging()
                .WithMetrics()
                .WithTracing();
        });

        var loggerProvider = sdk.LoggerProvider as LoggerProviderSdk;
        var meterProvider = sdk.MeterProvider as MeterProviderSdk;
        var tracerProvider = sdk.TracerProvider as TracerProviderSdk;

        Assert.NotNull(loggerProvider);
        Assert.NotNull(meterProvider);
        Assert.NotNull(tracerProvider);

        Assert.True(sdk.GetLoggerFactory() is not NullLoggerFactory);

        sdk.Dispose();

        Assert.True(loggerProvider.Disposed);
        Assert.True(meterProvider.Disposed);
        Assert.True(tracerProvider.Disposed);
    }
}
