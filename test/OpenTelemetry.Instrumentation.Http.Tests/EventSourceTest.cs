// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.Http.Implementation;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Instrumentation.Http.Tests;

public class EventSourceTest
{
    [Fact]
    public void EventSourceTest_HttpInstrumentationEventSource()
    {
        EventSourceTestHelper.MethodsAreImplementedConsistentlyWithTheirAttributes(HttpInstrumentationEventSource.Log);
    }
}