// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Instrumentation.SqlClient.Implementation;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Instrumentation.SqlClient.Tests;

public class EventSourceTest
{
    [Fact]
    public void EventSourceTest_SqlClientInstrumentationEventSource()
    {
        EventSourceTestHelper.MethodsAreImplementedConsistentlyWithTheirAttributes(SqlClientInstrumentationEventSource.Log);
    }
}
