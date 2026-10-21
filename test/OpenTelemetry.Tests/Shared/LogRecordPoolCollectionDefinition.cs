// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Tests;

/// <summary>
/// Serialization anchor for tests that depend on the process-global shared
/// <c>LogRecord</c> pool. Tests that resize the pool, or assert on its
/// identity or contents, should carry
/// <c>[Collection(LogRecordPoolCollectionDefinition.Name)]</c> so they run
/// sequentially and not concurrently with any other test which rents from or
/// returns to the pool.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
#pragma warning disable CA1515 // xUnit1027 requires [CollectionDefinition] classes to be public.
public sealed class LogRecordPoolCollectionDefinition
#pragma warning restore CA1515
{
    public const string Name = "LogRecordPool";
}
