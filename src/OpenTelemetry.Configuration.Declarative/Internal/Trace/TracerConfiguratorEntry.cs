// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// One <c>tracers</c> entry of the tracer configurator.
/// </summary>
/// <param name="Name">The tracer name pattern, where <c>*</c> and <c>?</c> are wildcards.</param>
/// <param name="Enabled">Whether matching tracers are enabled.</param>
/// <param name="Path">The document path of the entry.</param>
/// <param name="Position">The position of the entry.</param>
internal sealed record TracerConfiguratorEntry(
    string Name,
    bool Enabled,
    string Path,
    ConfigValuePosition Position);
