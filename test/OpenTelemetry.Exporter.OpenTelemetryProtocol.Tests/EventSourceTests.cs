// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.PersistentStorage.Abstractions;
using OpenTelemetry.PersistentStorage.FileSystem;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTest_OpenTelemetryProtocolExporterEventSource()
    {
        EventSourceTestHelper.MethodsAreImplementedConsistentlyWithTheirAttributes(OpenTelemetryProtocolExporterEventSource.Log);
    }

    [Fact]
    public void EventSourceTest_PersistentStorageAbstractionsEventSource()
    {
        EventSourceTestHelper.MethodsAreImplementedConsistentlyWithTheirAttributes(PersistentStorageAbstractionsEventSource.Log);
    }

    [Fact]
    public void EventSourceTest_PersistentStorageEventSource()
    {
        EventSourceTestHelper.MethodsAreImplementedConsistentlyWithTheirAttributes(PersistentStorageEventSource.Log);
    }
}
