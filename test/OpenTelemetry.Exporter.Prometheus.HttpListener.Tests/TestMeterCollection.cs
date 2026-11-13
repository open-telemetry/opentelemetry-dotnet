// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Prometheus.Tests;

/// <summary>
/// Serialization anchor for the test classes that create meters named
/// <c>test_meter</c>, whose name is embedded in their expected output and
/// snapshots. A <c>MeterProvider</c> subscribes to meters by name, so two such
/// tests running at the same time would each export the other's instruments.
/// Tests which create a meter with that name should carry
/// <c>[Collection(TestMeterCollection.Name)]</c> so they run sequentially with
/// each other, while the rest of the assembly still runs in parallel.
/// </summary>
[CollectionDefinition(Name)]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public sealed class TestMeterCollection
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
{
    public const string Name = "TestMeter";
}
