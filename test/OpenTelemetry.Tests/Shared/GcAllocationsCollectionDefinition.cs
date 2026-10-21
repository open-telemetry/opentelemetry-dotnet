// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Tests;

/// <summary>
/// Serialization anchor for tests that measure process-wide garbage collector
/// counters such as <see cref="GC.GetTotalAllocatedBytes(bool)"/>. Allocations
/// made by any test running at the same time are included in those counters,
/// so such tests should carry
/// <c>[Collection(GcAllocationsCollectionDefinition.Name)]</c> so they run
/// sequentially and not concurrently with any other test.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
#pragma warning disable CA1515 // xUnit1027 requires [CollectionDefinition] classes to be public.
public sealed class GcAllocationsCollectionDefinition
#pragma warning restore CA1515
{
    public const string Name = "GcAllocations";
}
