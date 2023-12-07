// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.AspNetCore.Implementation;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Instrumentation.AspNetCore.Tests;

public class EventSourceTest
{
    [Fact]
    public void EventSourceTest_AspNetCoreInstrumentationEventSource()
    {
        EventSourceTestHelper.MethodsAreImplementedConsistentlyWithTheirAttributes(AspNetCoreInstrumentationEventSource.Log);
    }
}