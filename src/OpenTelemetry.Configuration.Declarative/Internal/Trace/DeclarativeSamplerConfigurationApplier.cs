// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Applies the sampler of the document's <c>tracer_provider</c> section, unless a sampler set in
/// code replaces it.
/// </summary>
internal sealed class DeclarativeSamplerConfigurationApplier : IConfigureTracerProviderBuilder
{
    /// <inheritdoc/>
    public void ConfigureBuilder(IServiceProvider serviceProvider, TracerProviderBuilder tracerProviderBuilder)
    {
        if (serviceProvider.GetService<DeclarativeConfigurationDocumentAccessor>() is not { } accessor)
        {
            return;
        }

        var configuration = TracerProviderConfigurationBinder.Bind(accessor.GetDocument().Properties);
        if (configuration?.Sampler is not { } sampler)
        {
            return;
        }

        var placeholder = new DeclarativeSamplerPlaceholder();

        tracerProviderBuilder.SetSampler(placeholder);

        // Runs after all builder configuration and before application sampler callbacks.
        // Receives the placeholder unless a SetSampler call in code replaced it.
        tracerProviderBuilder.ConfigureSampler((_, selected) =>
        {
            if (ReferenceEquals(selected, placeholder))
            {
                return SamplerFactory.Create(sampler, serviceProvider);
            }

            OpenTelemetryDeclarativeConfigurationEventSource.Log.SamplerReplacedByCode(
                accessor.FilePath.DisplayPath,
                sampler.Path,
                sampler.Position.Line,
                sampler.Position.Column,
                selected.GetType().FullName ?? selected.GetType().Name);

            return selected;
        });
    }
}
