// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Extensions.Hosting.Implementation;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Extensions.Hosting.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTest_HostingExtensionsEventSource()
    {
        EventSourceTestHelper.MethodsAreImplementedConsistentlyWithTheirAttributes(HostingExtensionsEventSource.Log);
    }
}
