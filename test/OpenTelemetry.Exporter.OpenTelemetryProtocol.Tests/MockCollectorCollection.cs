// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
#pragma warning disable CA1515 // Consider making public types internal
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public sealed class MockCollectorCollection
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
#pragma warning restore CA1515 // Consider making public types internal
{
    public const string Name = "Mock collector";
}
