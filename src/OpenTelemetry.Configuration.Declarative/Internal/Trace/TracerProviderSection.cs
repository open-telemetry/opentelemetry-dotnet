// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// The parts of a document's <c>tracer_provider</c> section that this package applies.
/// </summary>
/// <remarks>
/// This is the typed result of <c>TracerProviderConfigurationBinder</c>, consumed by
/// <c>TracerProviderConfigurationApplier</c>. Sections this package does not apply are not modeled.
/// </remarks>
/// <param name="Processors">The span processors, in document order.</param>
/// <param name="Configurator">The tracer configurator, or <see langword="null"/> when omitted.</param>
/// <param name="IdGeneratorConfiguration">The ID generator configuration, or <see langword="null"/> when omitted or null.</param>
/// <param name="Sampler">The sampler, or <see langword="null"/> when omitted.</param>
internal sealed record TracerProviderSection(
    IReadOnlyList<SpanProcessorEntry> Processors,
    TracerConfiguratorSection? Configurator,
    ComponentConfiguration? IdGeneratorConfiguration,
    ComponentConfiguration? Sampler);
