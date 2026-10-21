// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Tests;

/// <summary>
/// Serialization anchor for tests that replace the process-global default
/// propagator via <c>Propagators.DefaultTextMapPropagator</c>. Tests that set
/// it should carry <c>[Collection(PropagatorsCollectionDefinition.Name)]</c>
/// so they run sequentially and not concurrently with any other test which
/// may read the default propagator.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
#pragma warning disable CA1515 // xUnit1027 requires [CollectionDefinition] classes to be public.
public sealed class PropagatorsCollectionDefinition
#pragma warning restore CA1515
{
    public const string Name = "Propagators";
}
