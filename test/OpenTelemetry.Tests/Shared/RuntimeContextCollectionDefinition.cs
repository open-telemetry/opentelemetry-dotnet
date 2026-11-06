// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Tests;

/// <summary>
/// Serialization anchor for tests that change the process-global
/// <c>RuntimeContext.ContextSlotType</c>. Any slot registered while a
/// different slot type is in effect (for example by the SDK starting an
/// activity on another thread) keeps that type after the original value is
/// restored, so tests that set it should carry
/// <c>[Collection(RuntimeContextCollectionDefinition.Name)]</c> so they run
/// sequentially and not concurrently with any other test.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
#pragma warning disable CA1515 // xUnit1027 requires [CollectionDefinition] classes to be public.
public sealed class RuntimeContextCollectionDefinition
#pragma warning restore CA1515
{
    public const string Name = "RuntimeContext";
}
