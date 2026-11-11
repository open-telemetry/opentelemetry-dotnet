// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Declarative-configuration YAML key names (snake_case, as they appear in the document).
/// </summary>
internal static class YamlKeys
{
    internal const string FileFormat = "file_format";
    internal const string Disabled = "disabled";
    internal const string LogLevel = "log_level";
    internal const string Resource = "resource";
    internal const string Attributes = "attributes";
    internal const string AttributesList = "attributes_list";
    internal const string SchemaUrl = "schema_url";
    internal const string Distribution = "distribution";
    internal const string Name = "name";
    internal const string Value = "value";
    internal const string Type = "type";
    internal const string TracerProvider = "tracer_provider";
    internal const string Processors = "processors";
    internal const string Exporter = "exporter";
    internal const string Simple = "simple";
    internal const string Batch = "batch";
    internal const string Console = "console";
    internal const string ScheduleDelay = "schedule_delay";
    internal const string ExportTimeout = "export_timeout";
    internal const string MaxQueueSize = "max_queue_size";
    internal const string MaxExportBatchSize = "max_export_batch_size";
    internal const string IdGenerator = "id_generator";
    internal const string Random = "random";
    internal const string DefaultConfig = "default_config";
    internal const string Tracers = "tracers";
    internal const string Config = "config";
    internal const string Enabled = "enabled";
    internal const string Sampler = "sampler";
    internal const string AlwaysOn = "always_on";
    internal const string AlwaysOff = "always_off";
    internal const string AlwaysRecord = "always_record";
    internal const string ParentBased = "parent_based";
    internal const string TraceIdRatioBased = "trace_id_ratio_based";
    internal const string Root = "root";
    internal const string RemoteParentSampled = "remote_parent_sampled";
    internal const string RemoteParentNotSampled = "remote_parent_not_sampled";
    internal const string LocalParentSampled = "local_parent_sampled";
    internal const string LocalParentNotSampled = "local_parent_not_sampled";
    internal const string Ratio = "ratio";

    /// <summary>
    /// Key names the schema marks as under development (with the <c>/development</c> suffix).
    /// When a key becomes stable the schema drops the suffix: move its constant to <see cref="YamlKeys"/>
    /// under the stable name, and the compiler reports every use that needs updating.
    /// </summary>
    internal static class Development
    {
        internal const string Instrumentation = "instrumentation/development";
        internal const string TracerConfigurator = "tracer_configurator/development";
    }
}
