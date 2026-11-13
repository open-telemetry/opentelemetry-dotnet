// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Tests;

/// <summary>
/// Serialization anchor for tests that assert on whether the process-global
/// legacy (empty-named) <see cref="System.Diagnostics.ActivitySource"/> has
/// listeners. Any <c>TracerProvider</c> built with a legacy source by a test
/// running at the same time adds such a listener, so those tests should carry
/// <c>[Collection(LegacyActivitySourceCollectionDefinition.Name)]</c> so they
/// run sequentially and not concurrently with any other test.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
#pragma warning disable CA1515 // xUnit1027 requires [CollectionDefinition] classes to be public.
public sealed class LegacyActivitySourceCollectionDefinition
#pragma warning restore CA1515
{
    public const string Name = "LegacyActivitySource";
}
