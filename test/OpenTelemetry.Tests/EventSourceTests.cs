// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;
using Xunit;

namespace OpenTelemetry.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTest_OpenTelemetrySdkEventSource()
    {
        EventSourceTestHelper.MethodsAreImplementedConsistentlyWithTheirAttributes(OpenTelemetrySdkEventSource.Log);
    }
}
