// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.Zipkin.Implementation;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Exporter.Zipkin.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTest_ZipkinExporterEventSource()
    {
        EventSourceTestHelper.MethodsAreImplementedConsistentlyWithTheirAttributes(ZipkinExporterEventSource.Log);
    }
}
