// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.Formatting;

/// <summary>
/// Provides helpers to format console output consistently.
/// </summary>
internal sealed class ConsoleFormatterContext()
{
    public Func<Resource> GetResource { get; set; } = () => throw new NotImplementedException();
}
