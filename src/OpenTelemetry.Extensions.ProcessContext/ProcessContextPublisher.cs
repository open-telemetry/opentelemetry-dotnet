// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if EXPOSE_EXPERIMENTAL_FEATURES
using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Internal;
#endif
using OpenTelemetry.Extensions.ProcessContext.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry;

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// Publishes the process context (OTEP 4719) for out-of-process readers such
/// as the OpenTelemetry eBPF Profiler, without going through a provider.
/// </summary>
/// <remarks>
/// <para experimental-warning="true"><b>WARNING</b>: This is an experimental API which might change or be removed in the future. Use at your own risk.</para>
/// <para>There is a single process context per process. Most applications should use the
/// <c>AddProcessContext</c> provider builder extensions instead, which publish the
/// provider's resolved resource; this type is for applications which manage the
/// published resource themselves. Mixing the two means the most recent call wins.</para>
/// <para>Only Linux on x64 and arm64 is supported. Elsewhere every member is a no-op.
/// No member throws: failures are reported through the
/// <c>OpenTelemetry-Extensions-ProcessContext</c> EventSource.</para>
/// </remarks>
[Experimental(DiagnosticDefinitions.ProcessContextExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
public
#else
/// <summary>
/// Publishes the process context (OTEP 4719) for out-of-process readers.
/// </summary>
internal
#endif
    static class ProcessContextPublisher
{
    /// <summary>
    /// Gets a value indicating whether process context publication is
    /// supported in this process.
    /// </summary>
    public static bool IsSupported => ProcessContextWriter.Default.IsSupported;

    /// <summary>
    /// Publishes <paramref name="resource"/>, or updates the published context
    /// if one already exists.
    /// </summary>
    /// <param name="resource">The resource to publish.</param>
    /// <returns><see langword="true"/> if the context is published.</returns>
    public static bool Publish(Resource resource)
        => Publish(resource, attributes: null);

    /// <summary>
    /// Publishes <paramref name="resource"/> together with additional
    /// attributes which are not part of the resource, or updates the
    /// published context if one already exists.
    /// </summary>
    /// <param name="resource">The resource to publish.</param>
    /// <param name="attributes">Additional attributes for external readers
    /// (the <c>ProcessContext.attributes</c> field). Supports the same value
    /// types as <see cref="Resource"/> attributes.</param>
    /// <returns><see langword="true"/> if the context is published.</returns>
    public static bool Publish(Resource resource, IEnumerable<KeyValuePair<string, object?>>? attributes)
    {
        if (resource == null || !ProcessContextWriter.Default.IsSupported)
        {
            return false;
        }

        try
        {
            return ProcessContextWriter.Default.Publish(ProcessContextSerializer.Serialize(resource, attributes));
        }
        catch (Exception ex)
        {
            ProcessContextEventSource.Log.PublishFailedOnce(ex);
            return false;
        }
    }

    /// <summary>
    /// Drops the published process context, if any.
    /// </summary>
    public static void Unpublish()
        => ProcessContextWriter.Default.Drop();
}
