// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Tests;

/// <summary>
/// Serialization anchor for tests that collect the SDK's own self-observability
/// metrics. The SDK meter is process-global, so those tests see the
/// instruments of every processor and exporter created by any test running at
/// the same time. Tests that subscribe to the SDK meter should carry
/// <c>[Collection(SelfObservabilityCollectionDefinition.Name)]</c> so they run
/// sequentially and not concurrently with any other test.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
#pragma warning disable CA1515 // xUnit1027 requires [CollectionDefinition] classes to be public.
public sealed class SelfObservabilityCollectionDefinition
#pragma warning restore CA1515
{
    public const string Name = "SelfObservability";
}
