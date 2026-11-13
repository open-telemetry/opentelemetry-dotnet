// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Tests;

/// <summary>
/// Serialization anchor for tests that listen to a process-global
/// <see cref="System.Diagnostics.Tracing.EventSource"/> and assert on the
/// events it emits. Any test running at the same time can cause events to be
/// emitted, so tests that create an <see cref="System.Diagnostics.Tracing.EventListener"/>
/// (for example via <c>TestEventListener</c> or <c>EventSourceTestHelper</c>)
/// should carry <c>[Collection(EventSourceCollectionDefinition.Name)]</c> so
/// they run sequentially and not concurrently with any other test.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
#pragma warning disable CA1515 // xUnit1027 requires [CollectionDefinition] classes to be public.
public sealed class EventSourceCollectionDefinition
#pragma warning restore CA1515
{
    public const string Name = "EventSource";
}
