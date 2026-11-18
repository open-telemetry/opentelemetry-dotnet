// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Applies the span processors a document's <c>tracer_provider</c> section defines when the tracer
/// provider is built.
/// </summary>
/// <remarks>
/// Runs at build time rather than at registration, because providers for processors and exporters
/// are resolved from the service provider and may be registered after
/// <c>UseDeclarativeConfiguration</c>.
/// </remarks>
internal static class TracerProviderConfigurationApplier
{
    /// <summary>
    /// Applies the document's span processors to <paramref name="builder"/>, in document order.
    /// </summary>
    /// <param name="serviceProvider">The services the tracer provider is built from.</param>
    /// <param name="builder">The tracer provider builder.</param>
    /// <exception cref="DeclarativeConfigurationException">
    /// The section names a component the package cannot build, or sets a stable setting it cannot
    /// apply. Unsupported settings are reported together before components are created.
    /// </exception>
    internal static void Configure(IServiceProvider serviceProvider, TracerProviderBuilder builder)
    {
        Guard.ThrowIfNull(serviceProvider);
        Guard.ThrowIfNull(builder);

        foreach (var processor in CreateProcessors(serviceProvider))
        {
            builder.AddProcessor(processor);
        }
    }

    /// <summary>
    /// Creates the document's span processors, in document order.
    /// </summary>
    /// <remarks>
    /// The caller owns the returned processors until ownership transfers to the tracer provider.
    /// </remarks>
    /// <param name="serviceProvider">The services the tracer provider is built from.</param>
    /// <returns>
    /// The processors, or an empty list when no document accessor is registered or the document has
    /// no <c>tracer_provider</c> section.
    /// </returns>
    /// <exception cref="DeclarativeConfigurationException">
    /// <inheritdoc cref="Configure(IServiceProvider, TracerProviderBuilder)" path="/exception"/>
    /// </exception>
    internal static IReadOnlyList<BaseProcessor<Activity>> CreateProcessors(IServiceProvider serviceProvider)
    {
        Guard.ThrowIfNull(serviceProvider);

        if (serviceProvider.GetService<DeclarativeConfigurationDocumentAccessor>() is not { } accessor)
        {
            return [];
        }

        // The document selected for this service provider, which is the one passed to
        // UseDeclarativeConfiguration unless configuration shared from elsewhere also carries one.
        var document = accessor.GetDocument();

        var configuration = TracerProviderConfigurationBinder.Bind(document.Properties);
        if (configuration is null)
        {
            return [];
        }

        var registry = serviceProvider.GetRequiredService<PluginComponentProviderRegistry>();

        // Validate support before creating components with resources or background workers.
        ValidateSupportedConfiguration(accessor.FilePath, document, configuration, registry);

        var processors = new List<BaseProcessor<Activity>>(configuration.Processors.Count);
        try
        {
            foreach (var entry in configuration.Processors)
            {
                processors.Add(CreateProcessor(accessor.FilePath, entry, registry));
            }
        }
        catch
        {
            // Clean up components not yet owned by the provider, preserving the original failure.
            foreach (var processor in processors)
            {
                ComponentCleanup.InvokeAndLogFailure(processor, () => processor.Shutdown(0));
                ComponentCleanup.InvokeAndLogFailure(processor, processor.Dispose);
            }

            throw;
        }

        return processors;
    }

    /// <summary>
    /// Returns the settings passed to the batch span processor's constructor for a <c>batch</c> entry.
    /// </summary>
    /// <remarks>
    /// A <c>schedule_delay</c> of 0 is replaced by 1 millisecond, with a warning, because core
    /// requires at least 1. A zero delay would make the export thread spin.
    /// </remarks>
    /// <param name="filePath">The document path, for diagnostics.</param>
    /// <param name="entry">A <c>batch</c> processor entry.</param>
    /// <returns>The settings the processor is constructed with.</returns>
    internal static BatchSpanProcessorSection GetEffectiveBatchSettings(FilePath filePath, SpanProcessorEntry entry)
    {
        var batch = entry.BatchSection ?? throw new ArgumentException("The entry is not a batch processor.", nameof(entry));

        if (batch.ScheduleDelayMilliseconds != 0)
        {
            return batch;
        }

        var position = entry.Processor.Properties.GetInt(YamlKeys.ScheduleDelay).Position;

        OpenTelemetryDeclarativeConfigurationEventSource.Log.ScheduleDelayZeroReplaced(
            filePath.DisplayPath,
            YamlPath.Child(entry.Processor.Path, YamlKeys.ScheduleDelay),
            position.Line,
            position.Column);

        return batch with { ScheduleDelayMilliseconds = 1 };
    }

    internal static BaseProcessor<Activity> CreateProcessor(
        FilePath filePath,
        SpanProcessorEntry entry,
        PluginComponentProviderRegistry registry)
    {
        if (entry.Exporter is not { } exporterConfiguration)
        {
            return registry.Create<BaseProcessor<Activity>>(entry.Processor.Name, entry.Processor.Properties);
        }

        // The processor owns the exporter; it is disposed here only if the processor is not created.
#pragma warning disable CA2000 // Dispose objects before losing scope
        var exporter = registry.Create<BaseExporter<Activity>>(exporterConfiguration.Name, exporterConfiguration.Properties);
#pragma warning restore CA2000
        try
        {
            if (entry.BatchSection is null)
            {
                return new SimpleActivityExportProcessor(exporter);
            }

            var batch = GetEffectiveBatchSettings(filePath, entry);

            return new BatchActivityExportProcessor(
                exporter,
                maxQueueSize: batch.MaxQueueSize,
                scheduledDelayMilliseconds: batch.ScheduleDelayMilliseconds,
                exporterTimeoutMilliseconds: batch.ExportTimeoutMilliseconds,
                maxExportBatchSize: batch.MaxExportBatchSize);
        }
        catch
        {
            ComponentCleanup.InvokeAndLogFailure(exporter, exporter.Dispose);
            throw;
        }
    }

    private static void ValidateSupportedConfiguration(
        FilePath filePath,
        DeclarativeConfigurationDocument document,
        TracerProviderSection configuration,
        PluginComponentProviderRegistry registry)
    {
        var unsupported = new List<(ConfigValuePosition Position, string Message)>();

        if (configuration.Sampler is { } sampler && !registry.Contains<Sampler>(sampler.Name))
        {
            unsupported.Add((sampler.Position, $"{YamlPath.Describe(sampler.Path, sampler.Position)} is a sampler"));
        }

        foreach (var property in document.UnappliedProperties)
        {
            if (SupportManifest.IsReportedByTracerProviderBuild(property))
            {
                unsupported.Add((property.Position, $"{YamlPath.Describe(property.Path, property.Position)} is a Stable setting"));
            }
        }

        // The SDK relies on the runtime's default random ID generation. Custom generators
        // (e.g. Activity.TraceIdGenerator) can only be set in code, not declarative config.
        if (configuration.IdGeneratorConfiguration is { } idGenerator && idGenerator.Name != YamlKeys.Random)
        {
            unsupported.Add((idGenerator.Position, $"{YamlPath.Describe(idGenerator.Path, idGenerator.Position)} is an ID generator"));
        }

        foreach (var entry in configuration.Processors)
        {
            if (entry.Exporter is { } exporter)
            {
                if (!registry.Contains<BaseExporter<Activity>>(exporter.Name))
                {
                    unsupported.Add((exporter.Position, $"{YamlPath.Describe(exporter.Path, exporter.Position)} is a span exporter"));
                }
            }
            else if (!registry.Contains<BaseProcessor<Activity>>(entry.Processor.Name))
            {
                unsupported.Add((entry.Processor.Position, $"{YamlPath.Describe(entry.Processor.Path, entry.Processor.Position)} is a span processor"));
            }
        }

        if (unsupported.Count == 0)
        {
            return;
        }

        var details = unsupported
            .OrderBy(u => u.Position.Line)
            .ThenBy(u => u.Position.Column)
            .Select(u => u.Message);

        throw new DeclarativeConfigurationException(
            $"The tracer provider cannot be built from '{filePath.DisplayPath}', because this version of the package does not support these settings: {string.Join("; ", details)}.");
    }
}
