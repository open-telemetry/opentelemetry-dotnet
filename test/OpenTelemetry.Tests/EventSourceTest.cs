// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;
using Xunit;

namespace OpenTelemetry.Tests;

[Collection("Uses-OpenTelemetrySdkEventSource")] // Prevent parallel execution with other tests that exercise the SdkEventSource
public class EventSourceTest
{
    [Fact]
    public void EventSourceTest_OpenTelemetrySdkEventSource()
    {
        EventSourceTestHelper.MethodsAreImplementedConsistentlyWithTheirAttributes(OpenTelemetrySdkEventSource.Log);
    }
}
