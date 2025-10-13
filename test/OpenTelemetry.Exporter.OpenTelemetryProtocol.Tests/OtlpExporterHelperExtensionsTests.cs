// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET462_OR_GREATER || NETSTANDARD2_0
#pragma warning disable CS0618 // Suppressing gRPC obsolete warning
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class OtlpExporterHelperExtensionsTests
{
    [Fact]
    public void OtlpExporter_Throws_OnGrpcWithDefaultFactory_ForTracing()
    {
        var services = new ServiceCollection();
        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddOtlpExporter(options => options.Protocol = OtlpExportProtocol.Grpc));

        using var sp = services.BuildServiceProvider();

        Assert.Throws<NotSupportedException>(() => sp.GetRequiredService<TracerProvider>());

        var tracerProviderBuilder = Sdk.CreateTracerProviderBuilder()
                                        .AddOtlpExporter(o => o.Protocol = OtlpExportProtocol.Grpc);

        Assert.Throws<NotSupportedException>(() => tracerProviderBuilder.Build());
    }

    [Fact]
    public void OtlpExporter_Throws_OnGrpcWithDefaultFactory_ForMetrics()
    {
        var services = new ServiceCollection();

        services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics.AddOtlpExporter(options => options.Protocol = OtlpExportProtocol.Grpc));

        using var sp = services.BuildServiceProvider();

        Assert.Throws<NotSupportedException>(() => sp.GetRequiredService<MeterProvider>());

        var meterProviderBuilder = Sdk.CreateMeterProviderBuilder()
                                    .AddOtlpExporter(o => o.Protocol = OtlpExportProtocol.Grpc);

        Assert.Throws<NotSupportedException>(() => meterProviderBuilder.Build());
    }

    [Fact]
    public void OtlpExporter_Throws_OnGrpcWithDefaultFactory_ForLogging()
    {
        var services = new ServiceCollection();

        services.AddOpenTelemetry()
            .WithLogging(builder => builder.AddOtlpExporter(options => options.Protocol = OtlpExportProtocol.Grpc));

        using var sp = services.BuildServiceProvider();

        Assert.Throws<NotSupportedException>(() => sp.GetRequiredService<ILoggerProvider>());

        Assert.Throws<NotSupportedException>(() => LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(logging =>
            {
                logging.AddOtlpExporter(o => o.Protocol = OtlpExportProtocol.Grpc);
            });
        }));
    }

    [Fact]
    public void OtlpExporter_DoesNotThrow_WhenCustomHttpClientFactoryIsSet_ForTraces()
    {
        var services = new ServiceCollection();

        services.AddOpenTelemetry()
            .WithTracing(builder =>
            {
                builder.AddOtlpExporter(options =>
                {
                    options.Protocol = OtlpExportProtocol.Grpc;
                    options.HttpClientFactory = () => new HttpClient();
                });
            });

        using var sp = services.BuildServiceProvider();

        var tracerProvider = sp.GetRequiredService<TracerProvider>();
        Assert.NotNull(tracerProvider);
    }

    [Fact]
    public void OtlpExporter_DoesNotThrow_WhenCustomHttpClientFactoryIsSet_ForMetrics()
    {
        var services = new ServiceCollection();

        services.AddOpenTelemetry()
            .WithMetrics(builder =>
            {
                builder.AddOtlpExporter(options =>
                {
                    options.Protocol = OtlpExportProtocol.Grpc;
                    options.HttpClientFactory = () => new HttpClient();
                });
            });

        using var sp = services.BuildServiceProvider();

        var meterProvider = sp.GetRequiredService<MeterProvider>();
        Assert.NotNull(meterProvider);
    }

    [Fact]
    public void OtlpExporter_DoesNotThrow_WhenCustomHttpClientFactoryIsSet_ForLogging()
    {
        var services = new ServiceCollection();

        services.AddOpenTelemetry()
            .WithLogging(builder =>
            {
                builder.AddOtlpExporter(options =>
                {
                    options.Protocol = OtlpExportProtocol.Grpc;
                    options.HttpClientFactory = () => new HttpClient();
                });
            });

        using var sp = services.BuildServiceProvider();

        var loggerProvider = sp.GetRequiredService<ILoggerProvider>();
        Assert.NotNull(loggerProvider);
    }
}
#pragma warning restore CS0618 // Suppressing gRPC obsolete warning
#endif
