// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Zipkin.Implementation;

internal readonly struct ZipkinAnnotation
{
    public ZipkinAnnotation(
        long timestamp,
        string value)
    {
        this.Timestamp = timestamp;
        this.Value = value;
    }

    public long Timestamp { get; }

    public string Value { get; }
}