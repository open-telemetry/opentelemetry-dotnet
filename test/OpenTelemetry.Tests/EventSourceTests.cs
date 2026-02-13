// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;
using Xunit;

namespace OpenTelemetry.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_OpenTelemetrySdkEventSource() =>
         EventSourceTestHelper.ValidateEventSourceIds<OpenTelemetrySdkEventSource>();
}
