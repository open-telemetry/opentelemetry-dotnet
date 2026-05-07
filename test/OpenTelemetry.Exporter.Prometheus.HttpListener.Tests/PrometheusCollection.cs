// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Exporter.Prometheus.Tests;

[CollectionDefinition(Name)]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public sealed class PrometheusCollection : ICollectionFixture<PrometheusFixture>
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
{
    public const string Name = "Prometheus";
}
