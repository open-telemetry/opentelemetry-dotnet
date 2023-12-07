// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;

namespace OpenTelemetry.Metrics;

[StructLayout(LayoutKind.Explicit)]
internal struct MetricPointValueStorage
{
    [FieldOffset(0)]
    public long AsLong;

    [FieldOffset(0)]
    public double AsDouble;
}
