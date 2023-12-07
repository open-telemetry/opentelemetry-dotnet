// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

namespace OpenTelemetry.Metrics;

/// <summary>
/// MeterProvider base class.
/// </summary>
public class MeterProvider : BaseProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MeterProvider"/> class.
    /// </summary>
    protected MeterProvider()
    {
    }
}