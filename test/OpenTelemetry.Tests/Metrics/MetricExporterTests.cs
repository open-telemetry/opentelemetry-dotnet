// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Metrics.Tests;

public class MetricExporterTests
{
    [Theory]
    [InlineData(ExportModes.Push)]
    [InlineData(ExportModes.Pull)]
    [InlineData(ExportModes.Pull | ExportModes.Push)]
    public void FlushMetricExporterTest(ExportModes mode)
    {
        BaseExporter<Metric>? exporter = null;

        switch (mode)
        {
            case ExportModes.Push:
                exporter = new PushOnlyMetricExporter();
                break;
            case ExportModes.Pull:
                exporter = new PullOnlyMetricExporter();
                break;
            case ExportModes.Pull | ExportModes.Push:
                exporter = new PushPullMetricExporter();
                break;
            default:
                throw new NotSupportedException($"Export mode '{mode}' is not supported");
        }

        var reader = new BaseExportingMetricReader(exporter);
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddReader(reader)
            .Build();

        switch (mode)
        {
            case ExportModes.Push:
                Assert.True(reader.Collect());
                Assert.True(meterProvider.ForceFlush());
                break;
            case ExportModes.Pull:
                Assert.False(reader.Collect());
                Assert.False(meterProvider.ForceFlush());
                Assert.True((exporter as IPullMetricExporter)?.Collect?.Invoke(-1) ?? false);
                break;
            case ExportModes.Pull | ExportModes.Push:
                Assert.True(reader.Collect());
                Assert.True(meterProvider.ForceFlush());
                break;
        }
    }

    [ExportModes(ExportModes.Push)]
    private sealed class PushOnlyMetricExporter : BaseExporter<Metric>
    {
        public override ExportResult Export(in Batch<Metric> batch)
        {
            return ExportResult.Success;
        }
    }

    [ExportModes(ExportModes.Pull)]
    private sealed class PullOnlyMetricExporter : BaseExporter<Metric>, IPullMetricExporter
    {
        public Func<int, bool>? Collect { get; set; }

        public override ExportResult Export(in Batch<Metric> batch)
        {
            return ExportResult.Success;
        }
    }

    [ExportModes(ExportModes.Pull | ExportModes.Push)]
    private sealed class PushPullMetricExporter : BaseExporter<Metric>
    {
        public override ExportResult Export(in Batch<Metric> batch)
        {
            return ExportResult.Success;
        }
    }
}
