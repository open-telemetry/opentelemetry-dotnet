// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Instrumentation.Propagators.Tests;

public class EventSourceTest
{
    [Fact]
    public void EventSourceTest_PropagatorsEventSource()
    {
        EventSourceTestHelper.MethodsAreImplementedConsistentlyWithTheirAttributes(OpenTelemetryPropagatorsEventSource.Log);
    }
}
