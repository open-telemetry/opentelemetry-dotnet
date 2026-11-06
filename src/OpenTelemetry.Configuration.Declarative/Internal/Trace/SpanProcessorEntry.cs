// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// A span processor entry of <c>tracer_provider/processors</c>.
/// </summary>
/// <remarks>
/// The binder supplies an exporter only for built-in simple and batch processors, and a batch
/// section only for a batch processor.
/// </remarks>
/// <param name="Processor">The processor, such as <c>simple</c> or <c>batch</c>.</param>
/// <param name="Exporter">The exporter of a <c>simple</c> or <c>batch</c> processor; otherwise <see langword="null"/>.</param>
/// <param name="BatchSection">The batch span processor section; otherwise <see langword="null"/>.</param>
internal sealed record SpanProcessorEntry(
    ComponentConfiguration Processor,
    ComponentConfiguration? Exporter,
    BatchSpanProcessorSection? BatchSection);
