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
#pragma warning disable xUnit1073 // Used by test projects that link this file, but not by every project that compiles it.
public sealed class GcAllocationsCollectionDefinition
#pragma warning restore xUnit1073
#pragma warning restore CA1515
{
    public const string Name = "GcAllocations";
}
