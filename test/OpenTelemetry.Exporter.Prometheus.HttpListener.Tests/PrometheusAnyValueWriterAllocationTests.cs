// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using OpenTelemetry.Exporter.Prometheus.Serialization;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.Prometheus.Tests;

[Collection(GcAllocationsCollectionDefinition.Name)]
public sealed class PrometheusAnyValueWriterAllocationTests
{
    [Fact]
    public void NestedBase64IsRejectedBeforeLegacyJsonReencoding()
    {
        // AppDomain counters include every thread, so this collection prevents other
        // tests from allocating concurrently. Monitoring remains enabled thereafter.
        AppDomain.MonitoringIsEnabled = true;
        var writer = new PrometheusAnyValueWriter(512 * 1024);
        var bytes = Convert.FromBase64String(new string('+', 256 * 1024));
        var map = new Dictionary<string, object?> { ["a"] = new object[] { bytes } };

        Assert.Equal("TRUNCATED", writer.ToLabelValueString("key", map));

        var before = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
        var result = writer.ToLabelValueString("key", map);
        var allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - before;

        Assert.Equal("TRUNCATED", result);
        Assert.True(allocated < 2 * 1024 * 1024, $"{allocated} bytes were allocated while rejecting nested Base64.");
    }
}
#endif
