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
        BaseExporter<Metric> exporter = null;

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
                Assert.True((exporter as IPullMetricExporter).Collect(-1));
                break;
            case ExportModes.Pull | ExportModes.Push:
                Assert.True(reader.Collect());
                Assert.True(meterProvider.ForceFlush());
                break;
        }
    }

    [ExportModes(ExportModes.Push)]
    private class PushOnlyMetricExporter : BaseExporter<Metric>
    {
        public override ExportResult Export(in Batch<Metric> batch)
        {
            return ExportResult.Success;
        }
    }

    [ExportModes(ExportModes.Pull)]
    private class PullOnlyMetricExporter : BaseExporter<Metric>, IPullMetricExporter
    {
        private Func<int, bool> funcCollect;

        public Func<int, bool> Collect
        {
            get => this.funcCollect;
            set { this.funcCollect = value; }
        }

        public override ExportResult Export(in Batch<Metric> batch)
        {
            return ExportResult.Success;
        }
    }

    [ExportModes(ExportModes.Pull | ExportModes.Push)]
    private class PushPullMetricExporter : BaseExporter<Metric>
    {
        public override ExportResult Export(in Batch<Metric> batch)
        {
            return ExportResult.Success;
        }
    }
}