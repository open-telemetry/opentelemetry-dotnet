// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry;

/// <summary>
/// Contains definitions for common processor pipeline weights.
/// </summary>
internal enum ProcessorPipelineWeight
{
    /// <summary>
    /// Pipeline start. Value: <see cref="int.MinValue"/>.
    /// </summary>
    PipelineStart = int.MinValue,

    /// <summary>
    /// Pipeline enrichment. Value: <c>-10000</c>.
    /// </summary>
    /// <remarks>
    /// Note: Enrichment processors which modify telemetry typically need to run
    /// before exporters.
    /// </remarks>
    PipelineEnrichment = -10_000,

    /// <summary>
    /// Pipeline middle. Value: <c>0</c>.
    /// </summary>
    PipelineMiddle = 0,

    /// <summary>
    /// Pipeline exporter. Value: <c>10000</c>.
    /// </summary>
    /// <remarks>
    /// Note: Export processors emitting telemetry typically need to run after
    /// enrichment processor.
    /// </remarks>
    PipelineExporter = 10_000,

    /// <summary>
    /// Pipeline end. Value: <see cref="int.MaxValue"/>.
    /// </summary>
    PipelineEnd = int.MaxValue,
}
