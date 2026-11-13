// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.ObjectModel;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Reads the parts of <c>tracer_provider</c> this package applies from a document's properties.
/// </summary>
/// <remarks>
/// Invalid input throws a <see cref="DeclarativeConfigurationException"/> that names the path and,
/// when known, the line and column of the offending value.
/// </remarks>
internal static class TracerProviderConfigurationBinder
{
    /// <summary>
    /// Binds the <c>tracer_provider</c> section of a document.
    /// </summary>
    /// <param name="root">The top-level properties of the document.</param>
    /// <returns>The bound section, or <see langword="null"/> when the document has no <c>tracer_provider</c>.</returns>
    /// <exception cref="DeclarativeConfigurationException">The section is invalid.</exception>
    internal static TracerProviderSection? Bind(ConfigProperties root)
    {
        if (!root.TryGetValue(YamlKeys.TracerProvider, out var value))
        {
            return null;
        }

        var tracerProvider = BindingHelpers.AsRequiredMapping(root, YamlKeys.TracerProvider, value, YamlKeys.TracerProvider);

        return new(
            BindProcessors(tracerProvider, root.GetKeyPosition(YamlKeys.TracerProvider)),
            BindTracerConfigurator(tracerProvider),
            BindIdGenerator(tracerProvider),
            BindSampler(tracerProvider));
    }

    private static ReadOnlyCollection<SpanProcessorEntry> BindProcessors(ConfigProperties tracerProvider, ConfigValuePosition sectionPosition)
    {
        var path = YamlPath.Child(YamlKeys.TracerProvider, YamlKeys.Processors);

        // Unlike the other tracer_provider children, processors is required.
        if (!tracerProvider.TryGetValue(YamlKeys.Processors, out var value))
        {
            throw BindingHelpers.CreateException(YamlKeys.TracerProvider, sectionPosition, $"requires '{YamlKeys.Processors}', a list of at least one span processor.");
        }

        var items = BindingHelpers.AsRequiredNonEmptySequence(tracerProvider, YamlKeys.Processors, value, path, "span processor");
        var processors = new List<SpanProcessorEntry>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            processors.Add(BindProcessor(items[i], YamlPath.Index(path, i)));
        }

        return processors.AsReadOnly();
    }

    private static SpanProcessorEntry BindProcessor(ConfigValue item, string path)
    {
        var processor = BindingHelpers.BindComponent(item, path, "span processor");

        // Only simple and batch are modeled; any other processor is kept by name so the caller
        // can report it as unsupported rather than failing the whole document.
        if (processor.Name is not (YamlKeys.Simple or YamlKeys.Batch))
        {
            return new(processor, null, null);
        }

        // A null simple or batch value has no exporter, which is required.
        var exporter = BindExporter(processor);
        var batch = processor.Name == YamlKeys.Batch ? BindBatch(processor) : null;

        return new(processor, exporter, batch);
    }

    private static ComponentConfiguration BindExporter(ComponentConfiguration processor)
    {
        var path = YamlPath.Child(processor.Path, YamlKeys.Exporter);

        if (!processor.Properties.TryGetValue(YamlKeys.Exporter, out var value) || value.Kind == ConfigValueKind.Null)
        {
            throw BindingHelpers.CreateException(processor.Path, processor.Position, $"requires '{YamlKeys.Exporter}'.");
        }

        return BindingHelpers.BindComponent(value, path, "span exporter", processor.Properties.GetKeyPosition(YamlKeys.Exporter));
    }

    private static BatchSpanProcessorSection BindBatch(ComponentConfiguration processor)
    {
        var scheduleDelay = BindingHelpers.ReadInteger(processor.Properties, processor.Path, YamlKeys.ScheduleDelay, BatchSpanProcessorSection.DefaultScheduleDelayMilliseconds, IntegerMinimum.Zero);
        var exportTimeout = BindingHelpers.ReadInteger(processor.Properties, processor.Path, YamlKeys.ExportTimeout, BatchSpanProcessorSection.DefaultExportTimeoutMilliseconds, IntegerMinimum.Zero);
        var maxQueueSize = BindingHelpers.ReadInteger(processor.Properties, processor.Path, YamlKeys.MaxQueueSize, BatchSpanProcessorSection.DefaultMaxQueueSize, IntegerMinimum.One);
        var maxExportBatchSize = BindingHelpers.ReadInteger(processor.Properties, processor.Path, YamlKeys.MaxExportBatchSize, BatchSpanProcessorSection.DefaultMaxExportBatchSize, IntegerMinimum.One);

        // A batch larger than the queue could never be filled. When only the queue size is set,
        // the user's value is what conflicts with the default batch size, so blame that key.
        if (maxExportBatchSize.Value > maxQueueSize.Value)
        {
            if (maxExportBatchSize.IsSpecified)
            {
                throw BindingHelpers.CreateException(
                    YamlPath.Child(processor.Path, YamlKeys.MaxExportBatchSize),
                    maxExportBatchSize.Position,
                    FormattableString.Invariant($"is {maxExportBatchSize.Value}, which is larger than '{YamlKeys.MaxQueueSize}' ({maxQueueSize.Value}). The batch size must be smaller than or equal to the queue size."));
            }

            throw BindingHelpers.CreateException(
                YamlPath.Child(processor.Path, YamlKeys.MaxQueueSize),
                maxQueueSize.Position,
                FormattableString.Invariant($"is {maxQueueSize.Value}, which is smaller than the default '{YamlKeys.MaxExportBatchSize}' ({maxExportBatchSize.Value}). Set '{YamlKeys.MaxExportBatchSize}' to no more than the queue size."));
        }

        return new(
            scheduleDelay.Value,
            exportTimeout.Value,
            maxQueueSize.Value,
            maxExportBatchSize.Value);
    }

    private static TracerConfiguratorSection? BindTracerConfigurator(ConfigProperties tracerProvider)
    {
        if (!tracerProvider.TryGetValue(YamlKeys.Development.TracerConfigurator, out var value))
        {
            return null;
        }

        var path = YamlPath.Child(YamlKeys.TracerProvider, YamlKeys.Development.TracerConfigurator);
        var configurator = BindingHelpers.AsRequiredMapping(tracerProvider, YamlKeys.Development.TracerConfigurator, value, path);

        return new(
            BindDefaultEnabled(configurator, path),
            BindTracerEntries(configurator, path));
    }

    // Tracers are enabled unless configured otherwise, so a missing default_config means enabled.
    private static bool BindDefaultEnabled(ConfigProperties configurator, string configuratorPath)
    {
        if (!configurator.TryGetValue(YamlKeys.DefaultConfig, out var value))
        {
            return true;
        }

        var path = YamlPath.Child(configuratorPath, YamlKeys.DefaultConfig);
        var defaultConfig = BindingHelpers.AsRequiredMapping(configurator, YamlKeys.DefaultConfig, value, path);

        return BindingHelpers.ReadEnabled(defaultConfig, path);
    }

    private static ReadOnlyCollection<TracerConfiguratorEntry> BindTracerEntries(ConfigProperties configurator, string configuratorPath)
    {
        // Omitting tracers is valid and means no per-tracer overrides.
        if (!configurator.TryGetValue(YamlKeys.Tracers, out var value))
        {
            return new List<TracerConfiguratorEntry>().AsReadOnly();
        }

        var path = YamlPath.Child(configuratorPath, YamlKeys.Tracers);
        var items = BindingHelpers.AsRequiredNonEmptySequence(configurator, YamlKeys.Tracers, value, path, "tracer entry");
        var tracers = new List<TracerConfiguratorEntry>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            tracers.Add(BindTracerEntry(items[i], YamlPath.Index(path, i)));
        }

        return tracers.AsReadOnly();
    }

    private static TracerConfiguratorEntry BindTracerEntry(ConfigValue item, string path)
    {
        if (item.Kind != ConfigValueKind.Mapping)
        {
            throw BindingHelpers.CreateException(path, item.Position, $"must be a mapping with '{YamlKeys.Name}' and '{YamlKeys.Config}'.");
        }

        var entry = item.AsMapping();

        var name = entry.GetString(YamlKeys.Name);
        if (name.Outcome != ConfigValueOutcome.Present)
        {
            throw BindingHelpers.CreateException(path, item.Position, $"requires '{YamlKeys.Name}', a string.");
        }

        // Core's AddSource rejects an empty or whitespace name when the provider is built.
        if (string.IsNullOrWhiteSpace(name.Value))
        {
            throw BindingHelpers.CreateException(YamlPath.Child(path, YamlKeys.Name), name.Position, "must not be empty or whitespace.");
        }

        if (!entry.TryGetValue(YamlKeys.Config, out var configValue) || configValue.Kind == ConfigValueKind.Null)
        {
            throw BindingHelpers.CreateException(path, item.Position, $"requires '{YamlKeys.Config}'.");
        }

        var configPath = YamlPath.Child(path, YamlKeys.Config);
        var config = BindingHelpers.AsRequiredMapping(entry, YamlKeys.Config, configValue, configPath);

        return new(name.Value!, BindingHelpers.ReadEnabled(config, configPath), path, item.Position);
    }

    private static ComponentConfiguration? BindIdGenerator(ConfigProperties tracerProvider)
    {
        if (!tracerProvider.TryGetValue(YamlKeys.IdGenerator, out var value) || value.Kind == ConfigValueKind.Null)
        {
            return null;
        }

        return BindingHelpers.BindComponent(
            value,
            YamlPath.Child(YamlKeys.TracerProvider, YamlKeys.IdGenerator),
            "ID generator",
            tracerProvider.GetKeyPosition(YamlKeys.IdGenerator));
    }

    private static ComponentConfiguration? BindSampler(ConfigProperties tracerProvider)
    {
        // An omitted sampler uses the default; an explicit null sampler is invalid.
        if (!tracerProvider.TryGetValue(YamlKeys.Sampler, out var value))
        {
            return null;
        }

        return BindingHelpers.BindComponent(
            value,
            YamlPath.Child(YamlKeys.TracerProvider, YamlKeys.Sampler),
            "sampler",
            tracerProvider.GetKeyPosition(YamlKeys.Sampler));
    }
}
