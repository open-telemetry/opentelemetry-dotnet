// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Extensions.ProcessContext.Internal;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Extensions.ProcessContext.Tests;

[Collection(EventSourceCollectionDefinition.Name)]
public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_ProcessContextEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<ProcessContextEventSource>();
}
