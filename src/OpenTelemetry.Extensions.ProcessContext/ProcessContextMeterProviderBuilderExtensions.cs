// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if EXPOSE_EXPERIMENTAL_FEATURES
using System.Diagnostics.CodeAnalysis;
#endif
using OpenTelemetry.Extensions.ProcessContext.Internal;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// Extension methods to publish the process context (OTEP 4719) from a
/// <see cref="MeterProviderBuilder"/>.
/// </summary>
/// <remarks>
/// <para experimental-warning="true"><b>WARNING</b>: This is an experimental API which might change or be removed in the future. Use at your own risk.</para>
/// </remarks>
[Experimental(DiagnosticDefinitions.ProcessContextExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
public
#else
/// <summary>
/// Extension methods to publish the process context (OTEP 4719) from a
/// <see cref="MeterProviderBuilder"/>.
/// </summary>
internal
#endif
    static class ProcessContextMeterProviderBuilderExtensions
{
    /// <summary>
    /// Publishes the meter provider's resolved resource as the process
    /// context once the provider is built, when enabled through the
    /// <c>OTEL_EXPERIMENTAL_PROCESS_CONTEXT_ENABLED</c> configuration key.
    /// </summary>
    /// <remarks>
    /// The context is dropped when the provider is disposed. Only Linux on
    /// x64 and arm64 is supported; elsewhere this is a no-op. Nothing is added
    /// to the telemetry hot path.
    /// </remarks>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
    public static MeterProviderBuilder AddProcessContext(this MeterProviderBuilder builder)
        => AddProcessContext(builder, configure: null);

    /// <summary>
    /// Publishes the meter provider's resolved resource as the process
    /// context once the provider is built.
    /// </summary>
    /// <remarks>
    /// <see cref="ProcessContextOptions.Enabled"/> defaults to the
    /// <c>OTEL_EXPERIMENTAL_PROCESS_CONTEXT_ENABLED</c> configuration key and
    /// can be set by <paramref name="configure"/>. The context is dropped when
    /// the provider is disposed. Only Linux on x64 and arm64 is supported;
    /// elsewhere this is a no-op. Nothing is added to the telemetry hot path.
    /// </remarks>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> being configured.</param>
    /// <param name="configure">Callback to configure <see cref="ProcessContextOptions"/>.</param>
    /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
    public static MeterProviderBuilder AddProcessContext(this MeterProviderBuilder builder, Action<ProcessContextOptions>? configure)
    {
        Guard.ThrowIfNull(builder);

        return builder.AddInstrumentation(sp => ProcessContextBuilderHelper.CreateRegistration(sp, configure));
    }
}
