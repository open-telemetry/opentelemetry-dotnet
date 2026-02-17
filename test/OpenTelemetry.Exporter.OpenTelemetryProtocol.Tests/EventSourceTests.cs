// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.PersistentStorage.Abstractions;
using OpenTelemetry.PersistentStorage.FileSystem;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_OpenTelemetryProtocolExporterEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<OpenTelemetryProtocolExporterEventSource>();

    [Fact]
    public void EventSourceTests_PersistentStorageAbstractionsEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<PersistentStorageAbstractionsEventSource>();

    [Fact]
    public void EventSourceTests_PersistentStorageEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<PersistentStorageEventSource>();
}
