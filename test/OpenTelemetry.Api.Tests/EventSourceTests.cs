// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Api.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTest_OpenTelemetryApiEventSource()
    {
        EventSourceTestHelper.MethodsAreImplementedConsistentlyWithTheirAttributes(OpenTelemetryApiEventSource.Log);
    }
}
