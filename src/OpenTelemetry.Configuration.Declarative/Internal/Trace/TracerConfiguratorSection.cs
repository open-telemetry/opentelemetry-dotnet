// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// The <c>tracer_configurator/development</c> section.
/// </summary>
/// <param name="DefaultEnabled">Whether a tracer that no entry matches is enabled.</param>
/// <param name="Tracers">The <c>tracers</c> entries, in document order.</param>
internal sealed record TracerConfiguratorSection(
    bool DefaultEnabled,
    IReadOnlyList<TracerConfiguratorEntry> Tracers);
