// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter;
using Xunit;

namespace OpenTelemetry.Metrics.Tests;

public class MeterProviderTests
{
    [Fact]
    public void MeterProviderFindExporterTest()
    {
        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddInMemoryExporter(exportedItems)
            .Build();

#pragma warning disable CA2000 // Dispose objects before losing scope
        Assert.True(meterProvider.TryFindExporter(out InMemoryExporter<Metric>? inMemoryExporter));
        Assert.False(meterProvider.TryFindExporter(out MyExporter? myExporter));
#pragma warning restore CA2000 // Dispose objects before losing scope
    }

    private sealed class MyExporter : BaseExporter<Metric>
    {
        public override ExportResult Export(in Batch<Metric> batch)
        {
            return ExportResult.Success;
        }
    }
}
