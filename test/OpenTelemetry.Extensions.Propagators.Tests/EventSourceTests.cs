// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Extensions.Propagators.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_OpenTelemetryPropagatorsEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<OpenTelemetryPropagatorsEventSource>();
}
