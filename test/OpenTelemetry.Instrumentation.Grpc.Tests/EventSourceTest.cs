// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.GrpcNetClient.Implementation;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Instrumentation.Grpc.Tests;

public class EventSourceTest
{
    [Fact]
    public void EventSourceTest_GrpcInstrumentationEventSource()
    {
        EventSourceTestHelper.MethodsAreImplementedConsistentlyWithTheirAttributes(GrpcInstrumentationEventSource.Log);
    }
}
