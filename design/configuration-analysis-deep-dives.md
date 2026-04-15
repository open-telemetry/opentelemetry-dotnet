# Configuration Analysis - Deep Dives

Reference material for specific implementation areas. Read on-demand alongside
the [executive summary](configuration-analysis.md).

**Date:** 2026-04-13 **Author:** Steve Gordon (with AI-assisted research)
**Driver:**
[open-telemetry/opentelemetry-dotnet#6380](https://github.com/open-telemetry/opentelemetry-dotnet/issues/6380)

---

## A. Options Classes Detail

### A.0 Summary Tables

**Options class inventory** - 15 classes across 5 packages. 11 use
`DelegatingOptionsFactory`; zero have reload support today.

| Class                                  | Package                          | Public | Env Vars | Named | DelegatingOptionsFactory | Reload Tier | Notes                                                |
| -------------------------------------- | -------------------------------- | ------ | -------- | ----- | ------------------------ | ----------- | ---------------------------------------------------- |
| `OtlpExporterOptions`                  | Exporter.OTLP                    | Yes    | 14       | Yes   | Yes (SP+name)            | Tier 2      | Endpoint/headers reloadable; protocol structural     |
| `OtlpExporterBuilderOptions`           | Exporter.OTLP                    | No     | 0        | Yes   | Yes (SP+name)            | -           | Per-signal OTLP orchestration                        |
| `ExperimentalOptions`                  | Exporter.OTLP                    | No     | 3        | Yes   | Yes (simple)             | Very High   | All get-only; needs setters for reload               |
| `SdkLimitOptions`                      | Exporter.OTLP                    | No     | 10       | No    | Yes (simple)             | Tier 1      | **Misplaced** - spec-level limits; should be in core |
| `BatchExportActivityProcessorOptions`  | OpenTelemetry                    | Yes    | 4        | Yes   | Yes (simple)             | Tier 1      | Export intervals reloadable                          |
| `BatchExportLogRecordProcessorOptions` | OpenTelemetry                    | Yes    | 4        | Yes   | Yes (simple)             | Tier 1      | Export intervals reloadable                          |
| `ActivityExportProcessorOptions`       | OpenTelemetry                    | Yes    | 0        | Yes   | Yes (SP+name)            | Tier 3      | Processor type structural                            |
| `LogRecordExportProcessorOptions`      | OpenTelemetry                    | Yes    | 0        | Yes   | Yes (SP+name)            | Tier 3      | Processor type structural                            |
| `MetricReaderOptions`                  | OpenTelemetry                    | Yes    | 0        | Yes   | Yes (SP+name)            | Tier 3      | Temporality structural                               |
| `PeriodicExportingMetricReaderOptions` | OpenTelemetry                    | Yes    | 2        | Yes   | Yes (simple)             | Tier 1      | Timer interval reloadable                            |
| `OpenTelemetryLoggerOptions`           | OpenTelemetry                    | Yes    | 0        | No    | No (ConfigBinder)        | Medium      | Reload disabled; mixes build-time + runtime state    |
| `ConsoleExporterOptions`               | Exporter.Console                 | Yes    | 0        | Yes   | No                       | N/A         | No IConfiguration ctor                               |
| `PrometheusAspNetCoreOptions`          | Exporter.Prometheus.AspNetCore   | Yes    | 0        | Yes   | No                       | Low/High    | Value props low effort; path structural              |
| `PrometheusHttpListenerOptions`        | Exporter.Prometheus.HttpListener | Yes    | 0        | Yes   | No                       | High        | UriPrefixes structural                               |
| `ZipkinExporterOptions`                | Exporter.Zipkin                  | Yes    | 1        | Yes   | Yes (SP+name)            | N/A         | Obsolete exporter                                    |

**Per-class reload candidacy** - no class has any reload support today.
Properties fall into three tiers (see
[S3.3](configuration-analysis.md#33-reload-tiers) for tier definitions).

| Options Class                          | Properties Reloadable?                        | Structural Properties?        | Current Reload | Effort        |
| -------------------------------------- | --------------------------------------------- | ----------------------------- | -------------- | ------------- |
| `OtlpExporterOptions`                  | Endpoint, Headers, Timeout: Yes. Protocol: No | Protocol, ExportProcessorType | None           | **High**      |
| `BatchExportActivityProcessorOptions`  | All 4 values: Yes                             | None                          | None           | **Medium**    |
| `BatchExportLogRecordProcessorOptions` | All 4 values: Yes                             | None                          | None           | **Medium**    |
| `PeriodicExportingMetricReaderOptions` | Both intervals: Yes                           | None                          | None           | **Medium**    |
| `ActivityExportProcessorOptions`       | ExportProcessorType: No                       | ExportProcessorType           | None           | **High**      |
| `LogRecordExportProcessorOptions`      | ExportProcessorType: No                       | ExportProcessorType           | None           | **High**      |
| `MetricReaderOptions`                  | TemporalityPreference: No                     | TemporalityPreference         | None           | **High**      |
| `OpenTelemetryLoggerOptions`           | Bool flags: Yes                               | None                          | **Disabled**   | **Medium**    |
| `SdkLimitOptions`                      | All limits: Yes (mutable)                     | None                          | None           | **Low**       |
| `ExperimentalOptions`                  | **None** (all get-only)                       | All                           | None           | **Very High** |

**Implemented spec env vars** - coverage is good; see [S2.4
gaps](configuration-analysis.md#24-spec-env-var-completeness) for what's
missing.

| Env Var                           | Spec Signal         | Status  | Notes                                     |
| --------------------------------- | ------------------- | ------- | ----------------------------------------- |
| `OTEL_SDK_DISABLED`               | General             | **Yes** | 3 provider builder bases                  |
| `OTEL_RESOURCE_ATTRIBUTES`        | General             | **Yes** | `OtelEnvResourceDetector.cs`              |
| `OTEL_SERVICE_NAME`               | General             | **Yes** | `OtelServiceNameEnvVarDetector.cs`        |
| `OTEL_TRACES_SAMPLER`             | Traces              | **Yes** | `TracerProviderSdk.cs`                    |
| `OTEL_TRACES_SAMPLER_ARG`         | Traces              | **Yes** | `TracerProviderSdk.cs`                    |
| `OTEL_BSP_*` (4 vars)             | Traces              | **Yes** | `BatchExportActivityProcessorOptions.cs`  |
| `OTEL_BLRP_*` (4 vars)            | Logs                | **Yes** | `BatchExportLogRecordProcessorOptions.cs` |
| `OTEL_ATTRIBUTE_*_LIMIT` (8 vars) | General/Traces/Logs | **Yes** | `SdkLimitOptions.cs`                      |
| `OTEL_METRIC_EXPORT_*` (2 vars)   | Metrics             | **Yes** | `PeriodicExportingMetricReaderOptions.cs` |
| `OTEL_METRICS_EXEMPLAR_FILTER`    | Metrics             | **Yes** | `MeterProviderSdk.cs`                     |
| `OTEL_EXPORTER_OTLP_*` (14+ vars) | All                 | **Yes** | `OtlpExporterOptions.cs`                  |
| `OTEL_EXPORTER_ZIPKIN_ENDPOINT`   | Traces              | **Yes** | `ZipkinExporterOptions.cs`                |

Per-class detail follows in sections A.1-A.11.

### A.1 OtlpExporterOptions

**File:**
`src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpExporterOptions.cs`
**Visibility:** public **Named options:** Partial (fresh instance when unnamed,
cached when named - see
[S2.1](configuration-analysis.md#21-configuration-infrastructure)) **Section
binding works:** Partial (delegate properties not bindable)
**DelegatingOptionsFactory used:** Yes **DI registration chain:**
`AddOtlpExporter()` -> `AddOtlpExporterSharedServices()` ->
`RegisterOptionsFactory(CreateOtlpExporterOptions)` ->
`IOptionsMonitor<OtlpExporterOptions>.Get(name)`

| Property | Type | Default | Env Var Key | Section-bindable? | Reload candidate? | Breaking change? |
| -------- | ----- | ------- | ---------- | --------------- | --------------- | ------------- |
| `Endpoint` | `Uri` | `http://localhost:4317` (gRPC) / `4318` (HTTP) | `OTEL_EXPORTER_OTLP_ENDPOINT` (+per-signal) | Yes | Yes | No |
| `Headers` | `string?` | `null` | `OTEL_EXPORTER_OTLP_HEADERS` (+per-signal) | Yes | Yes | No |
| `TimeoutMilliseconds` | `int` | `10000` | `OTEL_EXPORTER_OTLP_TIMEOUT` (+per-signal) | Yes | Yes | No |
| `Protocol` | `OtlpExportProtocol` | `Grpc` (.NET) / `HttpProtobuf` (NetFx) | `OTEL_EXPORTER_OTLP_PROTOCOL` (+per-signal) | Yes (enum) | Requires restart | Yes - changes transport |
| `UserAgentProductIdentifier` | `string?` | `string.Empty` | None | Yes | Yes | No |
| `ExportProcessorType` | `ExportProcessorType` | `Batch` | None | Yes (enum) | Requires restart | Yes - structural |
| `BatchExportProcessorOptions` | `BatchExportProcessorOptions<Activity>` | From DI | None | Partial (nested) | Requires restart | Yes - structural |
| `HttpClientFactory` | `Func<HttpClient>` | Internal default | None | **No** (delegate) | **No** | N/A |

**Per-signal env vars (via UseOtlpExporter path):**

| Property | Traces | Metrics | Logs |
| ------ | ----- | ------ | --- |
| Endpoint | `OTEL_EXPORTER_OTLP_TRACES_ENDPOINT` | `OTEL_EXPORTER_OTLP_METRICS_ENDPOINT` | `OTEL_EXPORTER_OTLP_LOGS_ENDPOINT` |
| Protocol | `OTEL_EXPORTER_OTLP_TRACES_PROTOCOL` | `OTEL_EXPORTER_OTLP_METRICS_PROTOCOL` | `OTEL_EXPORTER_OTLP_LOGS_PROTOCOL` |
| Headers | `OTEL_EXPORTER_OTLP_TRACES_HEADERS` | `OTEL_EXPORTER_OTLP_METRICS_HEADERS` | `OTEL_EXPORTER_OTLP_LOGS_HEADERS` |
| Timeout | `OTEL_EXPORTER_OTLP_TRACES_TIMEOUT` | `OTEL_EXPORTER_OTLP_METRICS_TIMEOUT` | `OTEL_EXPORTER_OTLP_LOGS_TIMEOUT` |

**Additional OTLP metrics env vars** (applied to `MetricReaderOptions`, not
`OtlpExporterOptions`):

- `OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE`
- `OTEL_EXPORTER_OTLP_METRICS_DEFAULT_HISTOGRAM_AGGREGATION`

**Notes:**

- `MtlsOptions` (`OtlpMtlsOptions?`) is internal, `#if NET` only. Reads
  `OTEL_EXPORTER_OTLP_CERTIFICATE`, `_CLIENT_CERTIFICATE`, `_CLIENT_KEY`.
- `AppendSignalPathToEndpoint` is internal. Set to `false` when per-signal
  endpoint is explicitly configured.
- `HttpClientFactory` default captures `this` and reads `TimeoutMilliseconds` at
  invocation time. `TryEnableIHttpClientFactoryIntegration` replaces it with a
  DI-backed delegate for traces/metrics (NOT logs - circular dependency).

### A.2 BatchExportActivityProcessorOptions (Traces)

**File:**
`src/OpenTelemetry/Trace/Processor/BatchExportActivityProcessorOptions.cs`
**Visibility:** public (inherits `BatchExportProcessorOptions<Activity>`)
**Named options:** Yes **Section binding works:** Yes (all primitive properties)
**DelegatingOptionsFactory used:** Yes **DI registration chain:**
`AddOpenTelemetryTracerProviderBuilderServices` -> `RegisterOptionsFactory` ->
`IOptionsFactory<BatchExportActivityProcessorOptions>`

| Property | Type | Default | Env Var Key | Section-bindable? | Reload candidate? | Breaking change? |
| -------- | ----- | ------- | ---------- | --------------- | --------------- | ------------- |
| `MaxQueueSize` | `int` | `2048` | `OTEL_BSP_MAX_QUEUE_SIZE` | Yes | Yes | No |
| `ScheduledDelayMilliseconds` | `int` | `5000` | `OTEL_BSP_SCHEDULE_DELAY` | Yes | Yes | No |
| `ExporterTimeoutMilliseconds` | `int` | `30000` | `OTEL_BSP_EXPORT_TIMEOUT` | Yes | Yes | No |
| `MaxExportBatchSize` | `int` | `512` | `OTEL_BSP_MAX_EXPORT_BATCH_SIZE` | Yes | Yes | No |

### A.3 BatchExportLogRecordProcessorOptions (Logs)

**File:**
`src/OpenTelemetry/Logs/Processor/BatchExportLogRecordProcessorOptions.cs`
**Visibility:** public (inherits `BatchExportProcessorOptions<LogRecord>`)
**Named options:** Yes **Section binding works:** Yes **DelegatingOptionsFactory
used:** Yes **DI registration chain:**
`AddOpenTelemetryLoggerProviderBuilderServices` -> `RegisterOptionsFactory`

| Property | Type | Default | Env Var Key | Section-bindable? | Reload candidate? | Breaking change? |
| -------- | ----- | ------- | ---------- | --------------- | --------------- | ------------- |
| `MaxQueueSize` | `int` | `2048` | `OTEL_BLRP_MAX_QUEUE_SIZE` | Yes | Yes | No |
| `ScheduledDelayMilliseconds` | `int` | `5000` | `OTEL_BLRP_SCHEDULE_DELAY` | Yes | Yes | No |
| `ExporterTimeoutMilliseconds` | `int` | `30000` | `OTEL_BLRP_EXPORT_TIMEOUT` | Yes | Yes | No |
| `MaxExportBatchSize` | `int` | `512` | `OTEL_BLRP_MAX_EXPORT_BATCH_SIZE` | Yes | Yes | No |

### A.4 ActivityExportProcessorOptions (Traces)

**File:** `src/OpenTelemetry/Trace/Processor/ActivityExportProcessorOptions.cs`
**Visibility:** public **Named options:** Yes **Section binding works:** Partial
(enum bindable, nested object not) **DelegatingOptionsFactory used:** Yes
(SP+name - resolves inner `BatchExportActivityProcessorOptions` by name) **DI
registration chain:** `AddOpenTelemetryTracerProviderBuilderServices` ->
`RegisterOptionsFactory(sp, config, name)`

| Property | Type | Default | Env Var Key | Section-bindable? | Reload candidate? | Breaking change? |
| -------- | ----- | ------- | ---------- | --------------- | --------------- | ------------- |
| `ExportProcessorType` | `ExportProcessorType` | `Batch` | None | Yes (enum) | Requires restart | Yes - structural |
| `BatchExportProcessorOptions` | `BatchExportActivityProcessorOptions` | From DI by name | None | No (complex) | N/A (container) | N/A |

### A.5 LogRecordExportProcessorOptions (Logs)

**File:** `src/OpenTelemetry/Logs/Processor/LogRecordExportProcessorOptions.cs`
**Visibility:** public **Named options:** Yes **Section binding works:** Partial
**DelegatingOptionsFactory used:** Yes (SP+name)

| Property | Type | Default | Env Var Key | Section-bindable? | Reload candidate? | Breaking change? |
| -------- | ----- | ------- | ---------- | --------------- | --------------- | ------------- |
| `ExportProcessorType` | `ExportProcessorType` | `Batch` | None | Yes (enum) | Requires restart | Yes - structural |
| `BatchExportProcessorOptions` | `BatchExportLogRecordProcessorOptions` | From DI by name | None | No (complex) | N/A (container) | N/A |

### A.6 MetricReaderOptions

**File:** `src/OpenTelemetry/Metrics/Reader/MetricReaderOptions.cs`
**Visibility:** public **Named options:** Yes **Section binding works:** Partial
**DelegatingOptionsFactory used:** Yes (SP+name - resolves inner
`PeriodicExportingMetricReaderOptions` by name)

| Property | Type | Default | Env Var Key | Section-bindable? | Reload candidate? | Breaking change? |
| -------- | ----- | ------- | ---------- | --------------- | --------------- | ------------- |
| `TemporalityPreference` | `MetricReaderTemporalityPreference` | `Cumulative` | None | Yes (enum) | Requires restart | Yes - structural |
| `PeriodicExportingMetricReaderOptions` | `PeriodicExportingMetricReaderOptions` | From DI by name | None | No (complex) | N/A (container) | N/A |

### A.7 PeriodicExportingMetricReaderOptions

**File:**
`src/OpenTelemetry/Metrics/Reader/PeriodicExportingMetricReaderOptions.cs`
**Visibility:** public **Named options:** Yes **Section binding works:** Yes
**DelegatingOptionsFactory used:** Yes

| Property | Type | Default | Env Var Key | Section-bindable? | Reload candidate? | Breaking change? |
| -------- | ----- | ------- | ---------- | --------------- | --------------- | ------------- |
| `ExportIntervalMilliseconds` | `int?` | `null` (exporter-dependent) | `OTEL_METRIC_EXPORT_INTERVAL` | Yes | Yes | No |
| `ExportTimeoutMilliseconds` | `int?` | `null` (exporter-dependent) | `OTEL_METRIC_EXPORT_TIMEOUT` | Yes | Yes | No |

**Note:** Nullable defaults are intentional - actual fallback values are
provided per-exporter at reader construction time (e.g., Console exporter uses
10000ms interval, `Timeout.Infinite` timeout).

### A.8 OpenTelemetryLoggerOptions

**File:** `src/OpenTelemetry/Logs/ILogger/OpenTelemetryLoggerOptions.cs`
**Visibility:** public **Named options:** No (reload explicitly disabled)
**Section binding works:** Yes (bound from `Logging:OpenTelemetry` section via
`ConfigurationBinder`) **DelegatingOptionsFactory used:** No (uses
`LoggerProviderOptions.RegisterProviderOptions`) **DI registration chain:**
`AddOpenTelemetryInternal` ->
`RegisterProviderOptions<OpenTelemetryLoggerOptions,
OpenTelemetryLoggerProvider>` -> `DisableOptionsReloading` ->
`Configure(callback)`

| Property | Type | Default | Env Var Key | Section-bindable? | Reload candidate? | Breaking change? |
| -------- | ----- | ------- | ---------- | --------------- | --------------- | ------------- |
| `IncludeFormattedMessage` | `bool` | `false` | None | Yes (via `Logging:OpenTelemetry`) | Yes (but disabled) | No |
| `IncludeScopes` | `bool` | `false` | None | Yes | Yes (but disabled) | No |
| `ParseStateValues` | `bool` | `false` | None | Yes | Yes (but disabled) | No |

**Internal properties:** `IncludeAttributes` (bool, default true),
`IncludeTraceState` (bool, default false), `ProcessorFactories` (list of
delegates), `ResourceBuilder` (nullable).

**Key design note:** `DisableOptionsReloading` freezes the options at first
resolution. This is an explicit workaround to prevent processors/exporters from
being re-created on configuration reload. To enable reload, the SDK would need
to propagate changes to already-constructed components without rebuilding them.

---

### A.9 ConsoleExporterOptions

**File:** `src/OpenTelemetry.Exporter.Console/ConsoleExporterOptions.cs`
**Visibility:** public **Named options:** Yes **Section binding works:** Yes
**DelegatingOptionsFactory used:** No (default `OptionsFactory<T>`) **DI
registration chain:** No explicit registration. User callbacks via
`services.Configure(name, configure)`.

| Property | Type | Default | Env Var Key | Section-bindable? | Reload candidate? | Breaking change? |
| -------- | ----- | ------- | ---------- | --------------- | --------------- | ------------- |
| `Targets` | `ConsoleExporterOutputTargets` | `Console` | None | Yes (enum) | Requires restart | Yes - structural |

### A.10 PrometheusAspNetCoreOptions

**File:**
`src/OpenTelemetry.Exporter.Prometheus.AspNetCore/PrometheusAspNetCoreOptions.cs`
**Visibility:** public **Named options:** Yes **Section binding works:** Partial
(delegates to internal `PrometheusExporterOptions`) **DelegatingOptionsFactory
used:** No

| Property | Type | Default | Env Var Key | Section-bindable? | Reload candidate? | Breaking change? |
| -------- | ----- | ------- | ---------- | --------------- | --------------- | ------------- |
| `ScrapeEndpointPath` | `string?` | `"/metrics"` | None | Yes | Requires restart | Yes - middleware route |
| `DisableTotalNameSuffixForCounters` | `bool` | `false` | None | Yes | Yes | No |
| `ScrapeResponseCacheDurationMilliseconds` | `int` | `300` | None | Yes | Yes | No |
| `DisableTimestamp` | `bool` | `false` | None | Yes | Yes | No |

### A.11 PrometheusHttpListenerOptions

**File:**
`src/OpenTelemetry.Exporter.Prometheus.HttpListener/PrometheusHttpListenerOptions.cs`
**Visibility:** public **Named options:** Yes **Section binding works:** Partial
(collection property) **DelegatingOptionsFactory used:** No

| Property | Type | Default | Env Var Key | Section-bindable? | Reload candidate? | Breaking change? |
| -------- | ----- | ------- | ---------- | --------------- | --------------- | ------------- |
| `ScrapeEndpointPath` | `string?` | `"/metrics"` | None | Yes | Requires restart | Yes - listener route |
| `DisableTotalNameSuffixForCounters` | `bool` | `false` | None | Yes | Yes | No |
| `DisableTimestamp` | `bool` | `false` | None | Yes | Yes | No |
| `UriPrefixes` | `IReadOnlyCollection<string>` | `["http://localhost:9464/"]` | None | Yes | Requires restart | Yes - listener binding |

### A.12 ZipkinExporterOptions

**File:** `src/OpenTelemetry.Exporter.Zipkin/ZipkinExporterOptions.cs`
**Visibility:** public sealed (**obsolete**) **Named options:** Yes **Section
binding works:** Partial (delegate + nested object not bindable)
**DelegatingOptionsFactory used:** Yes (SP+name)

| Property | Type | Default | Env Var Key | Section-bindable? | Reload candidate? | Breaking change? |
| -------- | ----- | ------- | ---------- | --------------- | --------------- | ------------- |
| `Endpoint` | `Uri` | `http://localhost:9411/api/v2/spans` | `OTEL_EXPORTER_ZIPKIN_ENDPOINT` | Yes | Requires restart | Yes - connection |
| `UseShortTraceIds` | `bool` | `false` | None | Yes | Yes | No |
| `MaxPayloadSizeInBytes` | `int?` | `4096` | None | Yes | Yes | No |
| `ExportProcessorType` | `ExportProcessorType` | `Batch` | None | Yes (enum) | Requires restart | Yes - structural |
| `BatchExportProcessorOptions` | `BatchExportProcessorOptions<Activity>` | From DI | None | No (complex) | N/A | N/A |
| `HttpClientFactory` | `Func<HttpClient>` | Default | None | **No** (delegate) | **No** | N/A |

---

### A.13 SdkLimitOptions

**File:**
`src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/SdkLimitOptions.cs`
**Visibility:** internal sealed **Named options:** No (singleton via
`.CurrentValue`) **DelegatingOptionsFactory used:** Yes (registered for
traces/logs only, NOT metrics)

| Property | Type | Default | Env Var Key | Section-bindable? | Reload candidate? | Breaking change? |
| -------- | ----- | ------- | ---------- | --------------- | --------------- | ------------- |
| `AttributeValueLengthLimit` | `int?` | `null` (no limit) | `OTEL_ATTRIBUTE_VALUE_LENGTH_LIMIT` | Yes | Yes | N/A (internal) |
| `AttributeCountLimit` | `int?` | `128` | `OTEL_ATTRIBUTE_COUNT_LIMIT` | Yes | Yes | N/A |
| `SpanAttributeValueLengthLimit` | `int?` | Falls back to `AttributeValueLengthLimit` | `OTEL_SPAN_ATTRIBUTE_VALUE_LENGTH_LIMIT` | Yes | Yes | N/A |
| `SpanAttributeCountLimit` | `int?` | Falls back to `AttributeCountLimit` | `OTEL_SPAN_ATTRIBUTE_COUNT_LIMIT` | Yes | Yes | N/A |
| `SpanEventCountLimit` | `int?` | `128` | `OTEL_SPAN_EVENT_COUNT_LIMIT` | Yes | Yes | N/A |
| `SpanLinkCountLimit` | `int?` | `128` | `OTEL_SPAN_LINK_COUNT_LIMIT` | Yes | Yes | N/A |
| `SpanEventAttributeCountLimit` | `int?` | Falls back to `SpanAttributeCountLimit` | `OTEL_EVENT_ATTRIBUTE_COUNT_LIMIT` | Yes | Yes | N/A |
| `SpanLinkAttributeCountLimit` | `int?` | Falls back to `SpanAttributeCountLimit` | `OTEL_LINK_ATTRIBUTE_COUNT_LIMIT` | Yes | Yes | N/A |
| `LogRecordAttributeValueLengthLimit` | `int?` | Falls back to `AttributeValueLengthLimit` | `OTEL_LOGRECORD_ATTRIBUTE_VALUE_LENGTH_LIMIT` | Yes | Yes | N/A |
| `LogRecordAttributeCountLimit` | `int?` | Falls back to `AttributeCountLimit` | `OTEL_LOGRECORD_ATTRIBUTE_COUNT_LIMIT` | Yes | Yes | N/A |

**Architectural concern:** This class lives in the OTLP exporter project, not in
the core SDK. Attribute/span/log limits are only enforced when using the OTLP
exporter. For declarative config, this likely needs to move to the core SDK. See
[SG](#g-sdklimitoptions-architecture-and-path-forward) for a full analysis and
non-breaking design options.

**Fallback chain pattern:** Signal-specific properties use a `bool xxxSet` flag
and fall back to the generic property if not explicitly set.

### A.14 ExperimentalOptions

**File:**
`src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/ExperimentalOptions.cs`
**Visibility:** internal sealed **Named options:** Yes (resolved by name, but
all properties are get-only) **DelegatingOptionsFactory used:** Yes

| Property | Type | Default | Env Var Key | Section-bindable? | Reload candidate? | Breaking change? |
| -------- | ----- | ------- | ---------- | --------------- | --------------- | ------------- |
| `EmitLogEventAttributes` | `bool` (get-only) | `false` | `OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EVENT_LOG_ATTRIBUTES` | **No** (no setter) | Requires restart | N/A |
| `EnableInMemoryRetry` | `bool` (get-only) | `false` | `OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY` = `"in_memory"` | **No** (no setter) | Requires restart | N/A |
| `EnableDiskRetry` | `bool` (get-only) | `false` | `OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY` = `"disk"` | **No** (no setter) | Requires restart | N/A |
| `DiskRetryDirectoryPath` | `string?` (get-only) | `null` | `OTEL_DOTNET_EXPERIMENTAL_OTLP_DISK_RETRY_DIRECTORY_PATH` | **No** (no setter) | Requires restart | N/A |

**Note:** All properties are get-only (set in constructor only). This makes
`ExperimentalOptions` completely non-bindable via `IConfiguration.Bind()` and
non-reloadable by design. Named options configure delegates cannot alter values.

### A.15 OtlpExporterBuilderOptions

**File:**
`src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Builder/OtlpExporterBuilderOptions.cs`
**Visibility:** internal sealed **Named options:** Yes (defaults to `"otlp"`
name) **DelegatingOptionsFactory used:** Yes (SP+name)

Contains four `OtlpExporterOptions` instances (Default, Logs, Metrics, Traces)
plus references to `SdkLimitOptions`, `ExperimentalOptions`, and per-signal
processor/reader options.

**Section binding:** `BindConfigurationToOptions` uses
`services.Configure<OtlpExporterBuilderOptions>(name, configuration)` which
calls `IConfiguration.Bind()`. Expected JSON structure:

```json
{
  "DefaultOptions": { "Endpoint": "...", "Protocol": "..." },
  "LoggingOptions": { "Endpoint": "..." },
  "MetricsOptions": { "Endpoint": "...", "TemporalityPreference": "..." },
  "TracingOptions": { "Endpoint": "..." }
}
```

The `DefaultOptions`/`LoggingOptions`/`MetricsOptions`/`TracingOptions` are
`IOtlpExporterOptions` getter-only properties that bind through to the
underlying `OtlpExporterOptions` sub-properties. Readonly fields
(`SdkLimitOptions`, `ExperimentalOptions`, etc.) are NOT bindable.

### A.16 OtlpTlsOptions / OtlpMtlsOptions

**Files:** `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpTlsOptions.cs`,
`OtlpMtlsOptions.cs` **Visibility:** internal (`#if NET` only) **Not registered
in DI** - instantiated inline by `OtlpExporterOptions.ApplyMtlsConfiguration()`

| Class | Property | Type | Env Var Key |
| ------ | -------- | ----- | ---------- |
| `OtlpTlsOptions` | `CaCertificatePath` | `string?` | `OTEL_EXPORTER_OTLP_CERTIFICATE` |
| `OtlpTlsOptions` | `EnableCertificateChainValidation` | `bool` | None (default `true`) |
| `OtlpMtlsOptions` | `ClientCertificatePath` | `string?` | `OTEL_EXPORTER_OTLP_CLIENT_CERTIFICATE` |
| `OtlpMtlsOptions` | `ClientKeyPath` | `string?` | `OTEL_EXPORTER_OTLP_CLIENT_KEY` |

### A.17 PrometheusExporterOptions

**File:**
`src/OpenTelemetry.Exporter.Prometheus.HttpListener/Internal/PrometheusExporterOptions.cs`
**Visibility:** internal sealed **Not directly exposed** - used internally by
both AspNetCore and HttpListener exporters. Properties delegated from public
options classes.

---

## B. DelegatingOptionsFactory Simplification

**File:** `src/Shared/Options/DelegatingOptionsFactory.cs` **Visibility:**
internal (shared linked file) **Origin:** Forked from `dotnet/runtime`
`OptionsFactory.cs` (commit `e13e7388`), extended with a delegate-based factory
function.

### Purpose

Replaces the stock `OptionsFactory<T>` so that options constructors can receive
`IConfiguration` (for env var / config binding) **before** any `Configure<T>()`
delegates run.

#### `Create(string name)` Flow

```text
1. TOptions options = optionsFactoryFunc(configuration, name)   <-- Factory delegate (reads IConfiguration/env vars)
2. foreach IConfigureOptions<T>  -> setup.Configure(name, options)  <-- Configure<T>() delegates override
3. foreach IPostConfigureOptions<T> -> post.PostConfigure(name, options)
4. foreach IValidateOptions<T> -> validate (throws on failure)
```

**Key insight:** The factory delegate runs *first*, establishing defaults from
env vars / `IConfiguration`. Consumer `Configure<T>()` delegates run *after*,
allowing overrides. This is the priority model: IConfiguration defaults ->
Configure delegates -> PostConfigure -> Validate.

#### Constructor Parameters

| Parameter | Type | Purpose |
| --------- | ----- | ------- |
| `optionsFactoryFunc` | `Func<IConfiguration, string, TOptions>` | Creates the options instance with IConfiguration + name |
| `configuration` | `IConfiguration` | Root configuration (injected from DI) |
| `setups` | `IEnumerable<IConfigureOptions<T>>` | Standard Configure delegates |
| `postConfigures` | `IEnumerable<IPostConfigureOptions<T>>` | Standard PostConfigure delegates |
| `validations` | `IEnumerable<IValidateOptions<T>>` | Standard Validate delegates |

**Named options behavior:** If a setup is `IConfigureNamedOptions<T>`,
`Configure(name, options)` is called. Unnamed `Configure<T>` only applies when
`name == Options.DefaultName`.

Microsoft.Extensions.Options is pulled in transitively via
`Microsoft.Extensions.Logging.Configuration`. The resolved versions per TFM are:

| Target Framework | M.E.Options | M.E.Options.ConfigurationExtensions |
| ---------------- | ----------- | ----------------------------------- |
| net8.0           | 8.0.0       | 8.0.0                               |
| net9.0           | 9.0.0       | 9.0.0                               |
| net10.0          | 10.0.0      | 10.0.0                              |

The dependency chain is: OpenTelemetry.csproj ->
Microsoft.Extensions.Logging.Configuration ->
Microsoft.Extensions.Options.ConfigurationExtensions ->
Microsoft.Extensions.Options.

The comment in DelegatingOptionsFactory.cs states that if the project takes a
dependency on Microsoft.Extensions.Options v5.0.0 or greater, much of the forked
code can be removed in favour of the
<https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.optionsfactory-1.createinstance?view=dotnet-plat-ext-5.0>
virtual method added in 5.0.0. The minimum transitive version across all modern
TFMs is now 8.0.0 - well above the 5.0.0 threshold. Even the
netstandard2.0/netstandard2.1/net462 TFMs would resolve 10.0.0 (there's no
downgrade override for those in Directory.Packages.props). So the condition the
comment describes has been met for every supported TFM for some time now, and
the DelegatingOptionsFactory simplification referenced in
<https://github.com/open-telemetry/opentelemetry-dotnet/pull/4093> should be
feasible.

The result would be roughly:

```c#
internal sealed class DelegatingOptionsFactory<TOptions> : OptionsFactory<TOptions>
    where TOptions : class
{
    private readonly Func<IConfiguration, string, TOptions> optionsFactoryFunc;
    private readonly IConfiguration configuration;

    public DelegatingOptionsFactory(
        Func<IConfiguration, string, TOptions> optionsFactoryFunc,
        IConfiguration configuration,
        IEnumerable<IConfigureOptions<TOptions>> setups,
        IEnumerable<IPostConfigureOptions<TOptions>> postConfigures,
        IEnumerable<IValidateOptions<TOptions>> validations)
        : base(setups, postConfigures, validations)
    {
        this.optionsFactoryFunc = optionsFactoryFunc;
        this.configuration = configuration;
    }

    protected override TOptions CreateInstance(string name)
        => optionsFactoryFunc(configuration, name);
}
```

---

## C. OTLP Exporter Snapshot Architecture and Reload Path

The OTLP exporter is the highest-value reload target (endpoint failover, auth
token rotation) and also the most structurally complex. This section maps the
exact points where options are materialised into immutable state, catalogues the
resulting barriers to reload, analyses three solution approaches and their
public-API implications, and identifies the one problem that no in-process
reload approach can solve cleanly.

### C.1 The Snapshot-Baking Chain

The chain from `IOptionsMonitor` through to the running exporter has five steps,
each of which copies values out of the `OtlpExporterOptions` object and discards
the reference:

```text
(A) Options resolved - snapshot fixed
    AddOtlpExporter(name=null) path:
      IOptionsFactory<OtlpExporterOptions>.Create(Options.DefaultName)
      -> fresh instance, configure delegate applied inline
      -> NOT tracked by IOptionsMonitor cache
    AddOtlpExporter(name=X) path:
      IOptionsMonitor<OtlpExporterOptions>.Get("X")
      -> cached instance, backed by IOptionsMonitor (OnChange fires for this name)

(B) Protocol baked into OtlpTraceExporter
    OtlpTraceExporter..ctor:
      this.startWritePosition = options.Protocol == Grpc ? 5 : 0
      (written once; field has no mechanism for later update)

(C) Transport type decided - HttpClient created
    OtlpExporterOptionsExtensions.GetExportClient():
      options.HttpClientFactory.Invoke()         -> HttpClient instance created once
      options.Protocol switch                    -> selects OtlpGrpcExportClient OR OtlpHttpExportClient
                                                    (different types; cannot be swapped without creating a new client)

(D) Values materialised in OtlpExportClient constructor
    OtlpExportClient..ctor(options, httpClient, signalPath):
      this.Endpoint = new UriBuilder(exporterEndpoint).Uri    <-- options.Endpoint consumed, reference dropped
      this.Headers  = options.GetHeaders<Dictionary<...>>(...)   <-- options.Headers parsed, reference dropped
      this.HttpClient = httpClient                            <-- captured for life of exporter

(E) Timeout baked into OtlpExporterTransmissionHandler
    OtlpExporterOptionsExtensions.GetExportTransmissionHandler():
      timeoutMilliseconds = httpClient.Timeout.TotalMilliseconds  <-- baked from HttpClient at construction
    OtlpExporterTransmissionHandler..ctor:
      this.TimeoutMilliseconds = timeoutMilliseconds              <-- stored; field never updated
```

After step D the `OtlpExporterOptions` object goes out of scope.
`OtlpExportClient.Endpoint` and `OtlpExportClient.Headers` are `internal`
read-only properties; `TimeoutMilliseconds` in the transmission handler is
`internal` and also never mutated. All three are effectively frozen for the
lifetime of the `ITracerProvider`.

### C.2 The Four Reload Barriers

#### C.2.1 `Endpoint` and `Headers` - Values Severed at Step D

After `OtlpExportClient` materialises these from `OtlpExporterOptions`, every
subsequent change to the options object is invisible to the running exporter.
Because both `Endpoint` and `Headers` are `internal` properties with no setters,
there is no in-place update path. Any solution must replace the export client
entirely.

#### C.2.2 `Protocol` - A Type Switch at Step C

`Protocol` controls which concrete type is constructed (`OtlpGrpcExportClient`
vs `OtlpHttpExportClient`) and whether `RequireHttp2` is true. It also sets
`startWritePosition` in `OtlpTraceExporter` (step B). Changing `Protocol` at
runtime would require replacing the transmission handler, the export client, and
the `startWritePosition` field inside the exporter simultaneously. Because
`OtlpTraceExporter.startWritePosition` and `transmissionHandler` are both
`private readonly` fields, neither can be updated without removing the
`readonly` modifier. This is the hardest barrier.

**Practical conclusion:** Protocol changes should be classified as a **Tier 3
restart-required** change
([S4.1](configuration-analysis.md#41-recommendations-for-step-2-config-provider-registration)).
No reload approach should attempt to support protocol switching without a
provider rebuild.

#### C.2.3 `TimeoutMilliseconds` - Baked Twice

The timeout ends up in two places: `HttpClient.Timeout` (step C) and
`OtlpExporterTransmissionHandler.TimeoutMilliseconds` (step E). The comment in
`OtlpExporterOptionsExtensions.GetExportTransmissionHandler()` explains that the
`HttpClient.Timeout` value is used as the authoritative source for HTTP clients.
When using the default `HttpClientFactory`, the `HttpClient` is created with the
configured timeout and is not modifiable afterwards. When using the
`IHttpClientFactory` integration, `CreateClient()` creates a new `HttpClient`
each time it is called - so recreating the export client on reload naturally
picks up a new timeout. The `TimeoutMilliseconds` on the transmission handler
would need to be made mutable (remove `readonly`) to support reload.

#### C.2.4 The Unnamed-Options Gap

The most common call pattern - `AddOtlpExporter()` with no name - bypasses
`IOptionsMonitor` entirely:

```csharp
// OtlpTraceExporterHelperExtensions.cs:74
exporterOptions = sp.GetRequiredService<IOptionsFactory<OtlpExporterOptions>>()
                    .Create(finalOptionsName);
configure?.Invoke(exporterOptions);
```

`IOptionsFactory.Create()` returns a fresh, uncached instance. It is never
registered with `IOptionsMonitor`'s change tracking. Even if the underlying
`IConfiguration` changes and `IOptionsMonitor.OnChange` fires, this instance
will never be notified, because it is not in the monitor's cache.

The named path (when `name != null`) does use `IOptionsMonitor.Get(name)`, which
participates in the options cache and will receive change notifications via
`OnChange`.

**Consequence:** The choice of whether to pass a name to `AddOtlpExporter()`
currently determines whether reload is structurally possible. Named options
support reload; unnamed options do not. This is an undocumented, non-obvious
behavioural split that any reload design must address or document explicitly.

To close this gap, the unnamed path would need to switch from
`IOptionsFactory.Create()` to `IOptionsMonitor.Get(Options.DefaultName)` - but
the comment in the code explains exactly why it does not: different signals
share `OtlpExporterOptions` and mixing their named configure delegates causes
cross-signal pollution (see
[opentelemetry-dotnet#4043](https://github.com/open-telemetry/opentelemetry-dotnet/issues/4043)).
Fixing the unnamed reload gap without re-introducing signal pollution requires
either per-signal option type separation or a different isolation mechanism.

### C.3 Solution Approaches

#### C.3.1 Approach A - Store `IOptionsMonitor` in `OtlpTraceExporter` (Monitor-Owns-Exporter)

The exporter holds `IOptionsMonitor<OtlpExporterOptions>` and subscribes to
`OnChange`. When a change fires it recreates the transmission handler and swaps
the reference atomically.

```csharp
// Internal shape - incorporates three-safeguard OnChange pattern
// (see Risk Register S2.2-S2.3) and swap-drain-dispose protocol (Risk Register S2.5)
internal class OtlpTraceExporter : BaseExporter<Activity>
{
    private volatile OtlpExporterTransmissionHandler transmissionHandler;
    private volatile int startWritePosition;
    private readonly IDisposable? changeSubscription;
    private readonly string optionsName;
    private readonly OtlpExportProtocol currentProtocol;
    private volatile bool disposed;
    private OtlpExporterOptions currentSnapshot;

    internal OtlpTraceExporter(
        IOptionsMonitor<OtlpExporterOptions> optionsMonitor, string name, ...)
    {
        this.optionsName = name;
        var opts = optionsMonitor.Get(name);
        this.currentSnapshot = opts;
        this.currentProtocol = opts.Protocol;
        this.transmissionHandler = opts.GetExportTransmissionHandler(...);
        this.startWritePosition  = opts.Protocol == Grpc ? 5 : 0;

        this.changeSubscription = optionsMonitor.OnChange((newOpts, n) =>
        {
            // Disposal guard: skip if exporter is shutting down
            if (this.disposed) return;

            // Name filter + value-equality guard: skip if not our name or no change
            if (n != this.optionsName) return;
            if (!HasMeaningfulChange(this.currentSnapshot, newOpts)) return;

            // Exception safety: catch, log, retain previous
            try
            {
                if (newOpts.Protocol != this.currentProtocol)
                {
                    // Protocol change is Tier 3 restart-required - log and ignore
                    OpenTelemetryProtocolExporterEventSource.Log.ExportClientReloadFailed(
                        "Protocol change requires restart; ignoring.");
                    return;
                }

                // Swap-drain-dispose protocol (see Risk Register S2.5)
                var newHandler = newOpts.GetExportTransmissionHandler(...);

                // Step 1: Atomic swap - new operations use new handler immediately
                var oldHandler = Interlocked.Exchange(ref this.transmissionHandler, newHandler);

                // Step 2: Drain - give in-flight exports time to complete
                oldHandler.Shutdown(drainTimeoutMilliseconds: 5000);

                // Step 3: Dispose - safe now; in-flight operations completed or timed out
                oldHandler.Dispose();

                this.currentSnapshot = newOpts;
                OpenTelemetryProtocolExporterEventSource.Log.ExportClientReloaded(
                    newOpts.Endpoint?.ToString());
            }
            catch (Exception ex)
            {
                OpenTelemetryProtocolExporterEventSource.Log.ExportClientReloadFailed(ex.Message);
                // Previous handler retained - no partial state
            }
        });
    }

    // Dispose subscription before internal resources
    protected override void Dispose(bool disposing)
    {
        this.disposed = true;
        this.changeSubscription?.Dispose();
        // ... then dispose transmissionHandler, etc.
        base.Dispose(disposing);
    }

    private static bool HasMeaningfulChange(OtlpExporterOptions current, OtlpExporterOptions incoming)
        => current.Endpoint != incoming.Endpoint
        || current.Headers != incoming.Headers
        || current.TimeoutMilliseconds != incoming.TimeoutMilliseconds;
}
```

**Public API implications:**

| Item | Breaking? | Notes |
| ----- | --------- | ------ |
| `OtlpTraceExporter(OtlpExporterOptions)` existing public constructor | No | Must be preserved; it creates a no-reload snapshot path |
| Adding internal `OtlpTraceExporter(IOptionsMonitor<OtlpExporterOptions>, string, ...)` | No | Internal; no public surface change |
| `transmissionHandler` field: `readonly` -> `volatile` | No | Internal field; no public surface |
| `startWritePosition` field: `readonly` -> `volatile` | No | Internal field; no public surface |
| `OtlpExporterTransmissionHandler.TimeoutMilliseconds`: `internal` property, currently get-only | No | Internal; adding set is not a public break |
| `IDisposable` subscription via `OnChange` | No | Internal only; `Dispose` calls `changeSubscription.Dispose()` before tearing down resources |

The public `OtlpTraceExporter(OtlpExporterOptions)` constructor is the key
constraint: it must survive unchanged because it is part of the public API and
is used by consumers who construct the exporter directly. It will simply not
participate in reload - acceptable because those callers own the options object
and can manage its lifetime themselves.

**Threading note ([Risk
S2.8](configuration-analysis-risks.md#28-onchange-callback-threading-model)):**
The `OnChange` callback performs potentially blocking work (HttpClient creation
in `GetExportTransmissionHandler`, drain wait in `Shutdown`). For the initial
implementation this is acceptable because OTLP configuration changes are rare
events (minutes/hours apart, not seconds). If profiling shows that the blocking
`OnChange` callback delays other `IOptionsMonitor` notifications unacceptably,
the expensive work (steps 1-3) should be offloaded to
`ThreadPool.QueueUserWorkItem` with a CAS-based sequence guard to handle rapid
successive changes.

**Gaps:** The DI-wired path still calls the processor factory lambda which calls
`BuildOtlpExporterProcessor` and passes a snapshot. To use Approach A, the
builder must be changed to resolve `IOptionsMonitor<OtlpExporterOptions>` from
DI and pass it (plus the name) through to the internal constructor. The
unnamed-options path ([SC.2.4](#c24-the-unnamed-options-gap)) must remain a
snapshot path and be explicitly documented as not supporting reload.

#### C.3.2 Approach B - Reloadable Wrapper Export Client

Instead of touching `OtlpTraceExporter`, introduce an internal wrapper that
implements `IExportClient` and holds a reference to the real client, swappable
on change:

```csharp
internal sealed class ReloadableOtlpExportClient : IExportClient
{
    private volatile IExportClient inner;

    internal ReloadableOtlpExportClient(IExportClient initial)
        => this.inner = initial;

    internal void Swap(IExportClient replacement)
        => this.inner = replacement;  // volatile write; readers see next access

    public ExportClientResponse SendExportRequest(...) => this.inner.SendExportRequest(...);
    public bool Shutdown(int t) => this.inner.Shutdown(t);
}
```

A coordinator (held by `OtlpTraceExporter` or an external helper) subscribes to
`IOptionsMonitor.OnChange`, builds a new `OtlpExportClient` from the updated
options, and calls `Swap()`. The `OtlpExporterTransmissionHandler` holds a
reference to the `ReloadableOtlpExportClient` (not the inner) and is unaware of
the swap.

**Public API implications:** The same as Approach A - no public API breaks.
Approach B additionally avoids the need to expose
`OtlpExporterTransmissionHandler` internals (the `TimeoutMilliseconds` still
needs to be updated somehow, which requires either making it mutable or using a
second indirection layer for the timeout).

**Downside:** The timeout lives in the transmission handler alongside the client
reference. To reload the timeout without replacing the transmission handler,
either a mutable property or a second indirection (a `Func<double> getTimeout`)
is required.

### C.4 HttpClient Lifecycle Under Reload

Both Approaches A and B recreate an `IExportClient` on options change, which
means creating a new `HttpClient` via `options.HttpClientFactory.Invoke()`. The
lifecycle risk depends on which factory is in use:

| Factory in use | Behaviour on reload | Risk |
| --- | --- | --- |
| Default factory (`new HttpClient(handler)` with timeout) | Old `HttpClient` disposed; new one created | Socket exhaustion if reload is frequent; `CancelPendingRequests()` must be called on old client before disposal to drain in-flight requests |
| `IHttpClientFactory` integration (`CreateClient(name)`) | Factory manages pooling; creating a new named client is cheap and safe | Low risk; `IHttpClientFactory` handles connection reuse and socket lifetime |
| User-supplied `Func<HttpClient>` | Invoked again; user owns the result | Unknown; user may not expect repeated calls |

For the **default factory** case, the sequence on reload must be:

1. Build new `OtlpExportClient` with new `HttpClient`
2. Atomically swap the reference (so new exports use new client)
3. Call `oldClient.CancelPendingRequests()` then `oldClient.Dispose()` after a
   brief drain window

The drain window is the open question: if an export is in-flight at swap time,
it is using the old `HttpClient`. Calling `CancelPendingRequests()` immediately
would abort it. A practical mitigation is to wait for the old transmission
handler's `Shutdown(drainTimeoutMs)` before disposing the old client, then
proceed regardless.

### C.5 Recommended Path for OTLP Reload (Tier 2)

Based on the above, the recommended approach for enabling hot-reload of
`Endpoint`, `Headers`, and `TimeoutMilliseconds` in the OTLP exporter is a
combination of Approaches A and B:

1. **Introduce `ReloadableOtlpExportClient`** (internal) as described in
   [SC.3.2](#c32-approach-b---reloadable-wrapper-export-client) - this decouples
   the transmission handler from client replacement and avoids making
   `OtlpExporterTransmissionHandler` mutable.

2. **Add an internal `OtlpTraceExporter` constructor** that accepts
   `IOptionsMonitor<OtlpExporterOptions>` and a named-options key. Wire it up
   from `BuildOtlpExporterProcessor` when the name is non-null (the
   named-options path).

3. **Make `OtlpExporterTransmissionHandler.TimeoutMilliseconds`** a mutable
   internal property. Update it atomically alongside the client swap.

4. **Leave the unnamed-options path unchanged.** Document it explicitly as not
   supporting runtime reload. The fix for the unnamed path
   ([SC.2.4](#c24-the-unnamed-options-gap)) is a separate concern that requires
   first resolving the per-signal isolation problem.

5. **Reject Protocol changes at reload time.** Log a warning via
   `OpenTelemetryProtocolExporterEventSource` and ignore the change. Document
   that `Protocol` is a Tier 3 restart-required setting.

6. **Preserve the public `OtlpTraceExporter(OtlpExporterOptions)`** constructor
   without modification.

7. **Apply the standard `OnChange` subscriber pattern** (see [Risk Register
   S2.2-S2.3](configuration-analysis-risks.md#22-onchange-subscription-lifecycle-and-disposal)):
   disposal guard, name + value-equality guard, exception safety wrapper. The
   [SC.3.1](#c31-approach-a---store-ioptionsmonitor-in-otlptraceexporter-monitor-owns-exporter)
   code sketch incorporates all three safeguards.

8. **Use swap-drain-dispose protocol** ([Risk
   S2.5](configuration-analysis-risks.md#25-disposal-race-during-component-swap---drain-semantics))
   for transmission handler replacement: atomic swap (new exports use the new
   handler immediately), bounded drain window (`Shutdown(5000)` on the old
   handler), then dispose. This prevents `ObjectDisposedException` /
   `TaskCanceledException` in in-flight exports.

This approach adds no breaking public API changes. The behaviour change (reload
support for named OTLP exporters) is strictly additive from a consumer
perspective.

For how OTLP reload fits within the broader telemetry policies design --
including how `TelemetryPolicyConfigurationProvider` drives the
`IOptionsMonitor<OtlpExporterOptions>` change notifications - see
[S4.4](configuration-analysis.md#44-telemetry-policies-architecture). OTLP
endpoint/token reload is classified in the build-order table in
[S4.5](configuration-analysis.md#45-recommended-build-order).

---

## D. Sampler Reloadability Design

### D.1 Current Architecture (Constraints)

Sampler configuration is read once at `TracerProviderSdk` construction time and
is fully immutable afterwards. The constraints are layered across three levels:

**`TracerProviderSdk` construction (lines 62 and 223-241):**

```csharp
// Called once at build time; result is stored in a get-only property
this.Sampler = GetSampler(serviceProvider.GetRequiredService<IConfiguration>(), state.Sampler);

// activityListener.Sample delegate is chosen by sampler type at startup
if (this.Sampler is AlwaysOnSampler)
    activityListener.Sample = (ref options) => AllDataAndRecorded;      // bypasses sampler entirely
else if (this.Sampler is AlwaysOffSampler)
    activityListener.Sample = (ref options) => PropagateOrIgnoreData;   // bypasses sampler entirely
else
    activityListener.Sample = (ref options) => ComputeActivitySamplingResult(ref options, this.Sampler);
```

`getRequestedDataAction` (used by the legacy `Activity` path) is assigned by the
same type switch and is also fixed for the lifetime of the provider.

**`TracerProviderSdk.Sampler` property:**

```csharp
internal Sampler Sampler { get; }   // get-only - no post-construction update path
```

**`TraceIdRatioBasedSampler` fields:**

```csharp
private readonly long idUpperBound;
private readonly double probability;
```

Both are `readonly` - the sampler is structurally immutable once constructed.

**`GetSampler` reads `IConfiguration` once:**

`IConfiguration.TryGetStringValue` is called for `OTEL_TRACES_SAMPLER` and
`OTEL_TRACES_SAMPLER_ARG` with no `IOptionsMonitor` or change-token
subscription. Any subsequent change to the underlying configuration source is
invisible to the running provider.

### D.2 The Gap

| Gap | Impact |
| --- | --- |
| No `SamplerOptions` class | No `IOptions<T>` integration; cannot bind sampler config from `appsettings.json` or any `IConfigurationSection` |
| No `ISamplerFactory` | Declarative config cannot resolve sampler by type name |
| Read once at startup | Live configuration changes (telemetry policies via OpAMP, file-watch, or custom sources) have no propagation path |
| Fast-path delegates fixed by type at startup | Even if `this.Sampler` were mutable, the `AlwaysOn`/`AlwaysOff` fast paths bypass the sampler field entirely |
| `getRequestedDataAction` fixed by type | Legacy `Activity` path would not pick up a sampler type change without reassigning the action |

### D.3 Implementation Approach - `ReloadableSampler` Wrapper

The cleanest path that avoids deep changes to `TracerProviderSdk` is a
reloadable wrapper that sits transparently in the existing fast-path selection
logic:

```csharp
internal sealed class ReloadableSampler : Sampler
{
    private volatile Sampler _inner;

    internal ReloadableSampler(Sampler initial) => _inner = initial;

    internal void UpdateSampler(Sampler replacement) => _inner = replacement; // volatile write

    public override SamplingResult ShouldSample(in SamplingParameters parameters)
        => _inner.ShouldSample(parameters);
}
```

Because `ReloadableSampler` is neither `AlwaysOnSampler` nor `AlwaysOffSampler`,
the type switch in `TracerProviderSdk` always routes it through the general
`else` branch:

```csharp
activityListener.Sample = (ref options) =>
    !Sdk.SuppressInstrumentation
        ? ComputeActivitySamplingResult(ref options, this.Sampler)   // this.Sampler == ReloadableSampler
        : ActivitySamplingResult.None;
```

`this.Sampler` is read on every activity creation (not captured at delegate
creation), so the `volatile` swap of `_inner` is visible immediately without
reassigning the delegate or the `getRequestedDataAction`. Both the
`ActivityListener.Sample` and the legacy `getRequestedDataAction =
RunGetRequestedDataOtherSampler` path call `this.Sampler.ShouldSample(...)`,
which delegates to the current `_inner`.

**`IOptionsMonitor<SamplerOptions>` wiring:**

The wiring applies the standard `OnChange` subscriber pattern (see [Risk
Register
S2.2-S2.3](configuration-analysis-risks.md#22-onchange-subscription-lifecycle-and-disposal))
-- disposal guard, value-equality guard, and exception safety. The
`ReloadableSampler` swap is inherently safe (no drain needed - `ShouldSample` is
a pure function with no resources to drain), so the pattern is simpler than the
OTLP exporter case:

```csharp
// Registered during TracerProviderBuilderSdk configuration
private IDisposable? samplerOptionsSubscription;
private volatile bool disposed;
private SamplerOptions currentSamplerSnapshot;

this.samplerOptionsSubscription = optionsMonitor.OnChange((newOpts, name) =>
{
    // Disposal guard
    if (this.disposed) return;

    // Value-equality guard
    if (newOpts.SamplerType == this.currentSamplerSnapshot.SamplerType
        && newOpts.SamplerArg == this.currentSamplerSnapshot.SamplerArg)
        return;

    // Exception safety
    try
    {
        var newInner = CreateSamplerFromOptions(newOpts);
        reloadableSampler.UpdateSampler(newInner);  // volatile reference swap - immediate
        this.currentSamplerSnapshot = newOpts;
        OpenTelemetrySdkEventSource.Log.TracerProviderSdkEvent(
            $"Sampler updated to '{newInner.GetType().Name}' from options change.");
    }
    catch (Exception ex)
    {
        // Previous sampler retained - no partial state
        OpenTelemetrySdkEventSource.Log.TracerProviderSdkEvent(
            $"Sampler reload failed, retaining current sampler: {ex.Message}");
    }
});

// In TracerProviderSdk.Dispose:
this.disposed = true;
this.samplerOptionsSubscription?.Dispose();
// ... then dispose sampler, processors, etc.
```

**Trade-offs vs. current behaviour:**

| Aspect | Current | With `ReloadableSampler` |
| --- | --- | --- |
| `AlwaysOnSampler` fast path | Active when sampler is `AlwaysOnSampler` | Lost - wrapper always takes the general path |
| `AlwaysOffSampler` fast path | Active when sampler is `AlwaysOffSampler` | Lost - wrapper always takes the general path |
| Per-activity call overhead | Zero allocations (delegate inline) | One extra virtual dispatch through the wrapper |
| Sampler type changes at runtime | Not supported | Supported - `UpdateSampler` replaces the inner completely |
| Thread safety | N/A | `volatile` write/read is sufficient for a reference swap |

The fast-path loss only applies when the sampler is configured via
`SamplerOptions` (options-based path). Callers who use `SetSampler(new
AlwaysOnSampler())` programmatically continue to get the fast path because the
type switch in `TracerProviderSdk` still fires for a concrete `AlwaysOnSampler`
reference.

### D.4 Justification Across Scenarios

Sampler reloadability is source-agnostic. The SDK-side infrastructure is written
once and serves all reload scenarios - only the policy source (the concrete
`IConfigurationProvider`) differs:

| Scenario | Policy source | SDK mechanism |
| --- | --- | --- |
| Telemetry Policies (OpAMP-backed) | OpAMP policy package translates effective configuration into `IConfiguration` keys | `IOptionsMonitor<SamplerOptions>.OnChange` |
| Telemetry Policies (file-based) | File-watching policy package monitors a policy file on disk | `IOptionsMonitor<SamplerOptions>.OnChange` |
| Declarative config hot reload | `OTEL_CONFIG_FILE` file changes on disk; file-watching `IConfigurationProvider` fires | `IOptionsMonitor<SamplerOptions>.OnChange` |
| Custom policy source | Consumer-implemented `IConfigurationProvider` against SDK abstractions | `IOptionsMonitor<SamplerOptions>.OnChange` |

Note: OpAMP is a transport for telemetry policies, not a separate scenario. An
OpAMP-backed policy source is one concrete implementation of the telemetry
policy abstractions defined in the core SDK ([Deep Dive
H.1](#h1-design-principle---abstractions-in-sdk-implementations-as-opt-in-packages)).

All scenarios converge on `IOptionsMonitor<SamplerOptions>` firing a change
notification and `ReloadableSampler.UpdateSampler` swapping the inner sampler.
The work is written once. For how each source type drives the same
`IConfigurationProvider` mechanism, the packaging model, and how this pattern
extends to other reload scenarios beyond sampling, see
[S4.4](configuration-analysis.md#44-telemetry-policies-architecture).

**Declarative config additionally requires `SamplerOptions` even without
reload.** The spec YAML includes sampler configuration:

```yaml
sdk:
  traces:
    sampler:
      type: parentbased_traceidratio
      arg: 0.5
```

The `ISamplerFactory` interface ([SE.2](#e2-factory-interface-design)) requires
a concrete options class to bind into. Without `SamplerOptions`, the factory
must do bespoke key-value parsing that breaks the unified env-var /
declarative-file symmetry established in
[SE.7](#e7-unifying-env-var-and-declarative-file-config). `SamplerOptions` is
therefore a **prerequisite for declarative config sampler support at startup**,
not just for runtime reload.

### D.5 Sequencing

`SamplerOptions` is a non-breaking addition with no runtime behaviour change and
is a prerequisite for both the declarative config and the reload work. The
reload plumbing (`IOptionsMonitor` wiring + `ReloadableSampler`) is the more
invasive piece and can be sequenced afterwards.

| Step | What | Blocks |
| --- | --- | --- |
| 1 | Add `SamplerOptions` with env-var constructor (`OTEL_TRACES_SAMPLER` / `OTEL_TRACES_SAMPLER_ARG`) following the `DelegatingOptionsFactory` pattern | Steps 2, 3 |
| 2 | Add `ISamplerFactory` and built-in factories (`always_on`, `always_off`, `traceidratio`, `parentbased_*`) | Declarative config sampler support |
| 3 | Wire `IOptionsMonitor<SamplerOptions>` in `TracerProviderSdk`; introduce `ReloadableSampler` | Live reload for all three scenarios |

Step 1 alone delivers `appsettings.json` section binding and the
`DelegatingOptionsFactory` priority model for free, with no reload risk. Steps 2
and 3 build on that foundation without requiring any revisit of step 1.

For the broader multi-scenario sequencing that extends this to export
enable/disable, SDK limits, batch intervals, and OTLP connection reload, see
[S4.5](configuration-analysis.md#45-recommended-build-order).

---

## E. Component Registry Detailed Design

This section expands on point 4 above. Java's `ComponentProvider` (via
`ServiceLoader` / SPI) handles both **discovery** (finding implementations via
classpath scanning) and **resolution** (mapping a string name to an
implementation). In .NET, discovery is replaced by explicit DI registration and
resolution is `IEnumerable<TFactory>` enumeration. The result is simpler,
AOT-safe, and consistent with how the rest of the .NET ecosystem handles
extensibility.

### E.1 Separation of Concerns

Two distinct problems must be solved, using different mechanisms:

| Problem | Mechanism |
| ------- | --------- |
| Parsing the config file into values | Custom `IConfigurationProvider` / `IConfigurationSource` |
| Resolving `"otlp"` -> a configured `BaseExporter<Activity>` | Named factory registry in DI |

The `IConfigurationProvider` approach (point 1 above) covers parsing - the YAML
file is read once and projected as a standard `IConfiguration` tree, making its
values available to any `IOptions<T>` binding. The **component factory
registry** covers resolution - it answers "given the string `otlp` and a config
subtree, give me a configured component". These two mechanisms compose: the
factory receives the `IConfiguration` subtree for its YAML node and uses it to
populate options, then constructs the component.

### E.2 Factory Interface Design

One interface per signal/component category. These live in the core
`OpenTelemetry` package (which already owns `BaseExporter<T>`, `Sampler`, etc.)
so exporter packages can depend on them without circular references:

```csharp
public interface ISpanExporterFactory
{
    string Name { get; }  // matches YAML key: "otlp", "zipkin", "console"
    BaseExporter<Activity> Create(IConfiguration configuration, IServiceProvider services);
}

public interface IMetricExporterFactory
{
    string Name { get; }
    BaseExporter<Metric> Create(IConfiguration configuration, IServiceProvider services);
}

public interface ILogRecordExporterFactory
{
    string Name { get; }
    BaseExporter<LogRecord> Create(IConfiguration configuration, IServiceProvider services);
}

public interface ISamplerFactory
{
    string Name { get; }
    Sampler Create(IConfiguration configuration, IServiceProvider services);
}

public interface ITextMapPropagatorFactory
{
    string Name { get; }
    TextMapPropagator Create(IConfiguration configuration, IServiceProvider services);
}
```

The `IConfiguration configuration` argument in each `Create` call is the
**subtree** rooted at that component's YAML node - not the root configuration.
For example, given:

```yaml
exporters:
  - otlp:
      endpoint: https://collector.example.com:4317
      protocol: grpc
```

...the `ISpanExporterFactory` for `"otlp"` receives an `IConfiguration` where
`configuration["endpoint"]` is `https://collector.example.com:4317`. The factory
does not need to know its position in the overall tree.

### E.3 Named Options Integration Inside Factories

Each factory should bind the `IConfiguration` subtree onto the component's
**existing options class** using `IOptions<T>`, rather than parsing bespoke
key-value pairs. This preserves the existing `DelegatingOptionsFactory` priority
model.

For each component node in the YAML tree, a deterministic, position-based
options name is assigned using the path through the tree - this keeps the
options state predictable in diagnostics and avoids GUID churn:

```text
"declarative:sdk:traces:processors:0:batch:exporter:otlp"
"declarative:sdk:traces:processors:1:batch:exporter:otlp"
"declarative:sdk:metrics:readers:0:periodic"
```

The YAML parsing phase (before SDK build) registers an
`IConfigureNamedOptions<TOtlpExporterOptions>` for each node, binding the
subtree values. When the factory runs, it resolves the pre-bound named options
instance:

```csharp
internal sealed class OtlpSpanExporterFactory : ISpanExporterFactory
{
    public string Name => "otlp";

    public BaseExporter<Activity> Create(IConfiguration configuration, IServiceProvider services)
    {
        // options name was registered during YAML parse phase; subtree already bound
        var optionsName = configuration[DeclarativeConfigKeys.OptionsName]!;
        var options = services.GetRequiredService<IOptionsMonitor<OtlpExporterOptions>>()
                              .Get(optionsName);
        return new OtlpTraceExporter(options);
    }
}
```

The binding happens via a named `IConfigureOptions<T>` registered during the
YAML walk, so it flows through the full `DelegatingOptionsFactory` pipeline:
YAML defaults -> `Configure<T>` delegates -> `PostConfigure` -> validate.
Programmatic `Configure<OtlpExporterOptions>()` calls registered by the user
still take precedence over YAML values, which is the correct and expected
override behaviour.

### E.4 Registration Model (AOT-Safe)

Java's `ServiceLoader` does runtime classpath scanning - incompatible with AOT.
The DI registration model is explicit and AOT-safe by construction: types are
directly referenced at registration time, no reflection-based discovery is
needed, and `TrimmerRootDescriptor` attributes are not required.

```csharp
// In OpenTelemetry.Exporter.OpenTelemetryProtocol
public static IServiceCollection AddOtlpExporterComponents(this IServiceCollection services)
{
    services.TryAddEnumerable(
        ServiceDescriptor.Singleton<ISpanExporterFactory, OtlpSpanExporterFactory>());
    services.TryAddEnumerable(
        ServiceDescriptor.Singleton<IMetricExporterFactory, OtlpMetricExporterFactory>());
    services.TryAddEnumerable(
        ServiceDescriptor.Singleton<ILogRecordExporterFactory, OtlpLogRecordExporterFactory>());
    return services;
}
```

`TryAddEnumerable` ensures idempotency - calling this multiple times from
different parts of the startup code does not produce duplicate registrations.

Users opt in explicitly:

```csharp
services.AddOpenTelemetry()
    .UseDeclarativeConfiguration()   // reads OTEL_CONFIG_FILE; registers YAML IConfigurationSource
    .AddOtlpExporterComponents();    // registers "otlp" factories for all three signals
```

| Aspect | Java SPI / ServiceLoader | .NET DI |
| ------ | -------------------- | ------- |
| Discovery | Runtime classpath scan | Explicit `services.AddXxx()` at startup |
| Reflection | Required | None |
| AOT safety | Broken without `@AutoService` workarounds | Safe |
| Third-party extensibility | Drop JAR on classpath | Call `services.TryAddEnumerable()` |
| Duplicate protection | None built-in | `TryAddEnumerable` |
| Debuggability | `ServiceLoader` is opaque | Standard DI diagnostics |

### E.5 Component Resolution at SDK Build Time

When `ITracerProvider` is being built, the declarative config system walks the
parsed YAML tree and resolves each component by enumerating registered
factories. This is synchronous and happens at DI container build time,
consistent with how `AddTracerProvider()` builder extensions work today.

```text
For sdk:traces:processors[]:
  For each node (e.g. "batch"):
    1. Resolve IEnumerable<ISpanProcessorFactory> from DI
    2. Find factory where Name == "batch" (throw if not found - fail fast)
    3. Call factory.Create(configSubtree, services)
    4. Register result with ITracerProviderBuilder

  For the nested exporter under a processor (e.g. "otlp"):
    1. Resolve IEnumerable<ISpanExporterFactory> from DI
    2. Find factory where Name == "otlp"
    3. Call factory.Create(configSubtree, services)
    4. Pass result to the processor factory
```

Unresolvable names (name not found in registered factories) should produce a
clear, actionable error at startup rather than a silent no-op, matching .NET
conventions for misconfigured DI.

### E.6 Multiple Instances of the Same Component Type

This is a capability that `OTEL_TRACES_EXPORTER` (single env var) cannot
express, but declarative config spec supports natively:

```yaml
processors:
  - batch:
      exporter:
        otlp:
          endpoint: https://collector-a.example.com:4317
  - batch:
      exporter:
        otlp:
          endpoint: https://collector-b.example.com:4317
```

Each YAML array position produces a separate options name (`..:processors:0:..`
and `..:processors:1:..`), each independently bound to a different
`IConfiguration` subtree. The `OtlpSpanExporterFactory` is invoked twice and
returns two independently configured exporters. Named options already support
this; no new plumbing is needed.

### E.7 Unifying Env Var and Declarative File Config

`OTEL_TRACES_EXPORTER=otlp` and a YAML file declaring a single OTLP exporter
should produce identical SDK behaviour and should share the same code path. The
factory registry unifies both:

**Env var path** (no `OTEL_CONFIG_FILE`):

```text
OTEL_TRACES_EXPORTER=otlp
  -> SDK reads env var via IConfiguration at build time
  -> Resolves IEnumerable<ISpanExporterFactory>; finds Name == "otlp"
  -> Calls Create(IConfiguration.GetSection("Otlp"), services)
  -> Exporter added to tracer provider
```

**Declarative file path** (`OTEL_CONFIG_FILE` is set):

```text
sdk.traces.exporters[0]:
  key = "otlp", subtree = { endpoint: ..., protocol: ... }
  -> YAML parsed to IConfiguration tree by IConfigurationProvider
  -> SDK walks tree; finds "otlp" exporter node
  -> Resolves IEnumerable<ISpanExporterFactory>; finds Name == "otlp"
  -> Calls Create(configSubtree, services)
  -> Exporter added to tracer provider
```

Both paths call the same factory. The only difference is how the
`IConfiguration` subtree arrives. This eliminates parallel code paths and
ensures that env-var-configured and file-configured exporters behave
identically.

### E.8 Third-Party Extensibility

Vendor packages add their own factories without any changes to the core SDK:

```csharp
// Vendor package
public static IServiceCollection AddMyVendorExporterComponents(this IServiceCollection services)
{
    services.TryAddEnumerable(
        ServiceDescriptor.Singleton<ISpanExporterFactory, MyVendorSpanExporterFactory>());
    return services;
}

internal sealed class MyVendorSpanExporterFactory : ISpanExporterFactory
{
    public string Name => "my_vendor";  // matches YAML key

    public BaseExporter<Activity> Create(IConfiguration configuration, IServiceProvider services)
    {
        var options = new MyVendorExporterOptions();
        configuration.Bind(options);  // NOT AOT-safe - uses ConfigurationBinder reflection; see SF.5
        return new MyVendorExporter(options);
    }
}
```

In YAML:

```yaml
exporters:
  - my_vendor:
      api_key: abc123
      region: us-east-1
```

No registration in any central registry, no PRs to the core SDK. The
`IEnumerable<ISpanExporterFactory>` resolution pattern automatically includes
any factory registered in DI - exactly the extension model .NET developers
expect from packages that integrate with `Microsoft.Extensions.*`.

> **AoT note:** `configuration.Bind(options)` in the example above is **not**
> AoT-safe - it calls `ConfigurationBinder` reflection under the hood. See
> [SF.5](#f5-required-fix-to-vendor-extensibility-pattern) for the required safe
> alternatives.

---

## F. AOT Compatibility Full Analysis

### F.1 Assessment Summary

The proposed design is **architecturally AoT-safe**: the factory registry
replaces Java's reflection-based `ServiceLoader` with explicit DI registration
by design ([SE.4](#e4-registration-model-aot-safe)). The reload mechanism itself
(`IChangeToken`, `IOptionsMonitor<T>.OnChange`, `ChangeToken.OnChange`) is
entirely delegate-based and AoT-safe.

The risk is at **options instantiation time** - triggered on both initial load
and every `IOptionsMonitor` reload. Two `IConfiguration.Bind()` usage points are
not safe and must be fixed: one is an existing bug in the codebase, the other is
the vendor extensibility pattern which must show the correct approach.

### F.2 What Is Safe

| Mechanism | Reason |
| --- | --- |
| Factory registry ([SE.4](#e4-registration-model-aot-safe)) - `services.TryAddEnumerable<ISpanExporterFactory, OtlpSpanExporterFactory>()` | Direct type references at registration time; no classpath scanning or reflection |
| `IEnumerable<ISpanExporterFactory>` DI resolution | Standard DI enumeration |
| `IOptionsMonitor<T>.Get(name)` / `OnChange` / `IChangeToken` | Delegate-based; no reflection |
| `OpenTelemetryConfigurationExtensions` helpers (`TryGetStringValue`, `TryGetIntValue`, etc.) | Use `configuration[key]` indexer - no `ConfigurationBinder` |
| Options constructors that read from `IConfiguration` via `configuration[key]` / helpers | Indexer access only; no reflection |
| `DelegatingOptionsFactory<T>` / `RegisterOptionsFactory<T>` / `DisableOptionsReloading<T>` | Annotated with `[DynamicallyAccessedMembers(PublicConstructors)]` on `#if NET` |
| Reload mechanism: `IChangeToken`, `OnChange` callbacks, `ChangeToken.OnChange` | Purely delegate-based |

### F.3 Current Bug: Reflection-Based Binding in `OtlpExporterBuilder.cs`

**File:**
`src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Builder/OtlpExporterBuilder.cs:153`

```csharp
// All four calls use IServiceCollection.Configure<T>(IConfiguration), which calls
// ConfigurationBinder.Bind() internally - reflection-based, not AOT-safe.
services.Configure<OtlpExporterBuilderOptions>(name, configuration);
services.Configure<LogRecordExportProcessorOptions>(
    name, configuration.GetSection(nameof(OtlpExporterBuilderOptions.LoggingOptions)));
services.Configure<MetricReaderOptions>(
    name, configuration.GetSection(nameof(OtlpExporterBuilderOptions.MetricsOptions)));
services.Configure<ActivityExportProcessorOptions>(
    name, configuration.GetSection(nameof(OtlpExporterBuilderOptions.TracingOptions)));
```

`IServiceCollection.Configure<T>(IConfiguration)` calls
`ConfigurationBinder.Bind()` internally, using `Type.GetProperties()`,
`PropertyInfo.SetValue()`, and `Activator.CreateInstance()` via reflection. All
four target types have nested object properties. Unlike
`OpenTelemetryLoggingExtensions.cs` (which has a documented suppression
justified by the type being primitives-only), there is **no**
`[UnconditionalSuppressMessage]` annotation here - this is an unmitigated
IL2026/IL3050 violation in AOT-published apps.

**Impact:** Fires on every options instantiation - both at startup and on each
`IOptionsMonitor` reload cycle.

**Fix (preferred):** Move bindings into the options constructors, following the
pattern all other SDK options already use. The `DelegatingOptionsFactory`
already supplies `IConfiguration` to the constructor - no factory wiring change
needed. Each options class reads its own keys explicitly via
`configuration[key]`, bypassing `ConfigurationBinder`.

**Fix (alternative):** Enable the `ConfigurationBinder` source generator
(`Microsoft.Extensions.Configuration.Binder` 8.0+), which generates AOT-safe
binding code at compile time and eliminates the reflection calls.

### F.4 Fragile Suppression: `OpenTelemetryLoggingExtensions.cs`

**File:** `src/OpenTelemetry/Logs/ILogger/OpenTelemetryLoggingExtensions.cs:249`

```csharp
[UnconditionalSuppressMessage("Trimming", "IL2026",
    Justification = "OpenTelemetryLoggerOptions contains only primitive properties.")]
[UnconditionalSuppressMessage("AOT", "IL3050",
    Justification = "OpenTelemetryLoggerOptions contains only primitive properties.")]
static void RegisterLoggerProviderOptions(IServiceCollection services)
```

The suppression is valid today - `OpenTelemetryLoggerOptions` only exposes
primitive `bool` properties. The SingletonOptionsManager refactoring (see
[S2.1](configuration-analysis.md#21-configuration-infrastructure) - removing
`ProcessorFactories` and `ResourceBuilder` from the options pipeline) keeps this
true and makes the suppression more defensible. However, it is a silent safety
net: adding a complex property in future would silently hide a real AoT
violation rather than produce a compiler warning.

**Long-term fix:** Adopt the `ConfigurationBinder` source generator and replace
the suppression with properly generated safe bindings. Alternatively, maintain
the constraint that `OpenTelemetryLoggerOptions` never holds complex types (the
direction of [S2.1](configuration-analysis.md#21-configuration-infrastructure)).

### F.5 Required Fix to Vendor Extensibility Pattern

The vendor example in [SE.8](#e8-third-party-extensibility) shows
`configuration.Bind(options)`. This is not AoT-safe. All factory `Create()`
implementations must use one of the following patterns instead.

**Option A - per-key reads via the `IConfiguration` indexer:**

```csharp
internal sealed class MyVendorSpanExporterFactory : ISpanExporterFactory
{
    public string Name => "my_vendor";

    public BaseExporter<Activity> Create(IConfiguration configuration, IServiceProvider services)
    {
        var options = new MyVendorExporterOptions();
        // IConfiguration["key"] is AOT-safe - a simple dictionary lookup, no reflection.
        var apiKey = configuration["api_key"];
        if (!string.IsNullOrEmpty(apiKey))
            options.ApiKey = apiKey;
        var region = configuration["region"];
        if (!string.IsNullOrEmpty(region))
            options.Region = region;
        return new MyVendorExporter(options);
    }
}
```

**Option B - `IConfiguration` constructor on the options class (preferred,
matches SDK convention):**

```csharp
internal sealed class MyVendorExporterOptions
{
    public MyVendorExporterOptions() { }

    // Called by the factory with the component's YAML subtree
    internal MyVendorExporterOptions(IConfiguration configuration)
    {
        var apiKey = configuration["api_key"];
        if (!string.IsNullOrEmpty(apiKey))
            ApiKey = apiKey;
        var region = configuration["region"];
        if (!string.IsNullOrEmpty(region))
            Region = region;
    }

    public string? ApiKey { get; set; }
    public string? Region { get; set; }
}

internal sealed class MyVendorSpanExporterFactory : ISpanExporterFactory
{
    public string Name => "my_vendor";

    public BaseExporter<Activity> Create(IConfiguration configuration, IServiceProvider services)
        => new MyVendorExporter(new MyVendorExporterOptions(configuration));
}
```

Option B is preferred: it separates configuration reading from factory logic,
keeps the options class independently testable, and matches
`OtlpExporterOptions`, `BatchExportActivityProcessorOptions`, and all other SDK
options classes.

> **`configuration.Bind(options)` must not appear in any factory `Create`
> implementation or in vendor guidance.** `IConfiguration["key"]` is always
> AOT-safe; `ConfigurationBinder.Bind()` is not.

---

## G. SdkLimitOptions Architecture and Path Forward

### G.1 Current State

`SdkLimitOptions` is an `internal sealed` class in
`OpenTelemetry.Exporter.OpenTelemetryProtocol`. It reads ten OTel-spec env vars
at construction time and exposes them as nullable `int` properties with a
cascading fallback chain:

```text
SpanEventAttributeCountLimit
  -> if not explicitly set: SpanAttributeCountLimit
       -> if not explicitly set: AttributeCountLimit (default 128)
```

The fallback uses `bool xxxSet` backing fields to distinguish three states: *not
configured*, *explicitly null* (no limit), and *explicitly set to a value*. This
is necessary because `null` is a meaningful value ("no limit"), not the same as
"not set".

**All ten env vars are already implemented**
([S2.4](configuration-analysis.md#24-spec-env-var-completeness)). The problem is
not missing configuration - it is where the class lives and where its limits are
enforced:

| Aspect | Current |
| ------ | ------- |
| Package | `OpenTelemetry.Exporter.OpenTelemetryProtocol` |
| Enforcement location | OTLP serializer (`ProtobufOtlpTraceSerializer`, `ProtobufOtlpLogSerializer`) |
| Enforcement timing | At export time, not at span/log creation time |
| Other exporters (Console, Zipkin, Prometheus) | **No limit enforcement** |
| In-memory span/log objects | Always hold full, unlimited data regardless of configured limits |

Zero usage of `SdkLimitOptions` (or any equivalent) exists in
`src/OpenTelemetry` (the core SDK package). Limits are exporter-local, not
SDK-wide.

### G.2 The Two Problems

There are two distinct problems, each with different complexity and risk:

#### Problem 1 - Architectural misplacement (the tractable problem)

The OTel spec intends limits as SDK-level behaviour: they should constrain what
the SDK records, regardless of which exporter is used. Spec-defined limit env
vars (`OTEL_ATTRIBUTE_COUNT_LIMIT`, etc.) are listed under [SDK Environment
Variables](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.25.0/specification/configuration/sdk-environment-variables.md),
not under exporter configuration. Because the class is in the OTLP exporter
package today, it is invisible to the core SDK, to other exporters, and to any
declarative config implementation that targets the SDK layer.

#### Problem 2 - Enforcement location (the breaking problem)

The spec also implies that limits should be enforced when spans and log records
are recorded, not when they are exported. Changing enforcement from
serialization-time to record-creation-time would:

- Change what `Activity.Tags`, `Activity.Events`, and `Activity.Links` contain
  in memory - code that inspects these after recording would see fewer items
- Affect all exporters simultaneously, not just OTLP
- Require changes to `TracerProviderSdk` and `LoggerProviderSdk` to intercept
  span/log creation

This is the genuinely breaking change. Problem 1 alone can be solved without it.

### G.3 Breaking Change Analysis

| Change | Breaking? | Details |
| ------ | --------- | ------- |
| Make `SdkLimitOptions` public | No | It is `internal sealed`; adding a public type is additive |
| Move limits type to core SDK package | **Yes** (package dependency change) | Core `OpenTelemetry` package gains a new options type; DI registration wiring changes |
| Change enforcement to record-creation time | **Yes** (behavioural) | In-memory span/log objects would be truncated at close, not just at serialization; all exporters affected |
| Keep enforcement in OTLP, expose limits to other exporters | No | Additive: other exporters gain optional limit reading, OTLP exporter behaviour unchanged |

The package move is "breaking" in the sense that it changes which NuGet package
owns the type and requires updating the DI registration site. Because the type
is `internal`, no external consumer can be directly broken - but the assembly
boundary change is still a structural breaking change under the project's
compatibility promises.

### G.4 Non-Breaking Design Options

Four designs can extend limit support without changing existing behaviour for
current OTLP exporter users.

#### Option A - Read-only interface bridge (least invasive)

Add a `ISdkLimits` interface to the core `OpenTelemetry` package. The OTLP
exporter's `SdkLimitOptions` implements it (it already exposes all the right
properties). Other exporters and the SDK itself resolve `ISdkLimits` from DI if
present; the OTLP exporter registers its instance as the implementation.

```csharp
// In OpenTelemetry (core SDK)
public interface ISdkLimits
{
    int? AttributeValueLengthLimit { get; }
    int? AttributeCountLimit { get; }
    int? SpanAttributeValueLengthLimit { get; }
    int? SpanAttributeCountLimit { get; }
    int? SpanEventCountLimit { get; }
    int? SpanLinkCountLimit { get; }
    int? SpanEventAttributeCountLimit { get; }
    int? SpanLinkAttributeCountLimit { get; }
    int? LogRecordAttributeValueLengthLimit { get; }
    int? LogRecordAttributeCountLimit { get; }
}
```

The interface is read-only (no setters), which makes future evolution safe. It
does not fix enforcement parity - Console/Zipkin/Prometheus would need to adopt
it - but it gives them an opt-in path. Declarative config can inject limit
values into `IConfiguration` under the existing env var keys, and
`SdkLimitOptions` picks them up automatically through its existing constructor.

**Complexity:** Low. **Risk:** Low. **Enforcement parity:** No (OTLP only).
**Unblocks declarative config:** Yes, for OTLP.

#### Option B - Public `SdkLimitsOptions` in core SDK alongside the internal one

Add a new public `SdkLimitsOptions` class to `OpenTelemetry` following the
existing `DelegatingOptionsFactory` pattern. The OTLP-internal `SdkLimitOptions`
checks DI for the public class at construction and defers to it; if absent it
falls back to its own env var reading (preserving current behaviour exactly).

```csharp
// In OpenTelemetry (core SDK) - new public type
public sealed class SdkLimitsOptions
{
    public int? AttributeValueLengthLimit { get; set; }
    public int? AttributeCountLimit { get; set; }
    // ... all ten properties as simple auto-props, no fallback chain
}

// In OTLP exporter - updated constructor
internal SdkLimitOptions(IConfiguration configuration, SdkLimitsOptions? sdkLimits = null)
{
    if (sdkLimits is not null) { /* copy values from sdkLimits */ }
    else { /* existing env var reading */ }
}
```

The fallback chain semantics remain in the OTLP-internal class. The public class
is a flat bag of nullable values that section-binding can populate directly.

**Complexity:** Low-Medium. **Risk:** Low. **Enforcement parity:** No (OTLP
only, until other exporters adopt). **Unblocks declarative config:** Yes.

#### Option C - Limits-enforcement processor (spec-correct, long-term right answer)

Add `WithSpanLimits(...)` / `WithLogLimits(...)` to `TracerProviderBuilder` /
`LoggerProviderBuilder`. These insert a built-in limiting processor into the
pipeline:

```csharp
builder.WithTracing(b => b
    .AddSource("MyApp")
    .WithSpanLimits(limits =>
    {
        limits.AttributeCountLimit = 64;
        limits.SpanEventCountLimit = 32;
    })
    .AddOtlpExporter());
```

The processor enforces limits at span end. The OTLP exporter, when a SDK-level
limiting processor is detected, delegates to it and skips its own
serialization-time enforcement. For users who do not call `WithSpanLimits`, the
OTLP exporter continues enforcing limits at serialization time as today (no
surprise breakage).

This is the architecture the OTel spec intends. It makes limit enforcement
exporter-agnostic and aligns the .NET SDK with the Java and Go SDK
implementations.

**Complexity:** High. **Risk:** Medium (requires SDK pipeline changes).
**Enforcement parity:** Yes. **Unblocks declarative config:** Yes.

#### Option D - IConfiguration key constants in core SDK (minimal, immediate)

Define well-known `IConfiguration` key constants in the core SDK:

```csharp
// In OpenTelemetry (core SDK)
public static class SdkConfigurationKeys
{
    public const string AttributeCountLimit = "OTEL_ATTRIBUTE_COUNT_LIMIT";
    public const string SpanAttributeCountLimit = "OTEL_SPAN_ATTRIBUTE_COUNT_LIMIT";
    // ...
}
```

Any exporter reads these from `IConfiguration` by convention. No class moves, no
behavioral changes, no enforcement parity. Declarative config injects values
into `IConfiguration` under these keys; `SdkLimitOptions` picks them up
automatically through its existing constructor.

**Complexity:** Minimal. **Risk:** None. **Enforcement parity:** No. **Unblocks
declarative config:** Yes, for OTLP. This is a discovery / documentation
improvement more than an architectural one.

### G.5 Sequencing

The two problems (architectural misplacement vs. enforcement location) should be
sequenced independently. Trying to fix enforcement location and package
placement in one step is the thing that makes this complex.

| Step | What | Breaking? | Blocks |
| ----- | ----- | --------- | ------ |
| 1 | Option D: define `IConfiguration` key constants in core SDK | No | Nothing - immediate unblocking for declarative config |
| 2 | Option B: add public `SdkLimitsOptions` to core SDK with `DelegatingOptionsFactory` | No | Other exporters adopting limits |
| 2a | Register `PostConfigure<SdkLimitsOptions>` for the fallback cascade chain ([Risk S1.5](configuration-analysis-risks.md#15-postconfigure-gap-for-fallback-chains-under-reload)) | No | Correct cascade behaviour under reload and `Configure<T>` delegates |
| 3 | Other exporters (`Console`, `Zipkin`) read from `SdkLimitsOptions` if registered | No | Enforcement parity at export time |
| 4 | Option C: add `WithSpanLimits` / `WithLogLimits` processor to core SDK | No (opt-in) | Spec-correct enforcement at record time |
| 5 | (Future) deprecate OTLP-internal `SdkLimitOptions` once SDK-level enforcement is adopted | Structural | Full spec compliance |

Step 1 alone is sufficient to unblock declarative config for OTLP exporter
users. Steps 2-3 extend that to other exporters without any behavioral change
for existing users. **Step 2a is critical for reload correctness:** the
cascading fallback chain (`SpanEventAttributeCountLimit` ->
`SpanAttributeCountLimit` -> `AttributeCountLimit`) currently runs in the
constructor before `Configure<T>` delegates execute. Moving it to
`PostConfigure<T>` ensures that user-supplied `Configure` delegates and
declarative config values are reflected in the cascade ([Risk
S1.5](configuration-analysis-risks.md#15-postconfigure-gap-for-fallback-chains-under-reload)).
Step 4 is the spec-correct long-term destination but is independent and can be
sequenced well after the declarative config milestone.

---

## H. Telemetry Policies Architecture

Telemetry policies (OTEP #4738) define *what* can change at runtime (sampling
rate, export enable/disable, SDK limits, etc.). The *source* of those changes --
an OpAMP agent, a file on disk, an HTTP endpoint, or a custom channel - is an
implementation detail that varies by deployment.

### H.1 Design Principle - Abstractions in SDK, Implementations as Opt-In Packages

The SDK should own the **abstractions and core plumbing**:

- The `TelemetryPolicyConfigurationProvider` /
  `TelemetryPolicyConfigurationSource` base types (or interfaces)
- `IOptionsMonitor<T>` change propagation infrastructure (the `OnChange`
  subscriber pattern from [S4.5 Step
  0b](configuration-analysis.md#45-recommended-build-order))
- Validation (`IValidateOptions<T>`) and the reject-invalid-retain-previous
  contract

Concrete policy sources should be **separate NuGet packages** that consumers opt
into:

| Package                                                         | Contents                                                                                                             | Extra Dependencies                     | Rationale                                                                                                         |
| --------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------- | -------------------------------------- | ----------------------------------------------------------------------------------------------------------------- |
| `OpenTelemetry` (core SDK)                                      | Policy abstractions, `IConfigurationProvider`/`IConfigurationSource` base, `OnChange` subscriber pattern             | None                                   | Every reload consumer needs these                                                                                 |
| `OpenTelemetry.Extensions.TelemetryPolicies.File` *(name TBD)*  | File-watching `IConfigurationProvider` (YAML/JSON policy file)                                                       | None beyond what the SDK already takes | Low risk to bundle in core if desired - no new dependencies - but a separate package keeps the SDK minimal        |
| `OpenTelemetry.Extensions.TelemetryPolicies.OpAMP` *(name TBD)* | OpAMP-backed `IConfigurationProvider` that translates OpAMP effective configuration into `IConfiguration` key/values | OpAMP .NET client library              | **Must** be a separate package - the OpAMP client dependency should never be forced on consumers who don't use it |

This separation means:

- A consumer using OpAMP adds the OpAMP policy package and calls a builder
  extension (e.g., `.WithOpAmpPolicySource(options => ...)`).
- A consumer using file-based policies adds the file policy package and calls a
  different builder extension.
- A consumer with a custom policy source (proprietary management plane, HTTP
  poller, etc.) implements the abstractions directly - the SDK does not prevent
  this.
- Consumers who don't need runtime policy changes take no additional
  dependencies.

### H.2 Java Concept Mapping

| Java abstraction    | .NET equivalent                                                                            | Status                                                  |
| ------------------- | ------------------------------------------------------------------------------------------ | ------------------------------------------------------- |
| `PolicyProvider`    | `TelemetryPolicyConfigurationSource` + concrete `IConfigurationProvider` (per source type) | Abstractions need building; implementations per package |
| `PolicyValidator`   | Options constructor + `DelegatingOptionsFactory` pipeline                                  | Already exists (11 classes)                             |
| `PolicyStore`       | `IOptionsMonitor<T>`                                                                       | Already exists (free from M.E.Options)                  |
| `PolicyImplementer` | `IOptionsMonitor<T>.OnChange(...)` subscriber                                              | Needs wiring (zero listeners today)                     |
| `DelegatingSampler` | `ReloadableSampler`                                                                        | Designed; not built                                     |

The most significant simplification over Java is eliminating `PolicyStore`.
`IOptionsMonitor<SamplerOptions>` already *is* the store - it holds the current
value and provides selective dispatch to listeners.

### H.3 End-to-End Flow

The flow is identical regardless of policy source - only the top box changes:

```text
Policy source change (OpAMP push, file write, HTTP poll response, custom)
    |
    v
Concrete IConfigurationProvider.UpdatePolicies(newValues)   <-- lives in source-specific package
    |   Data = newValues; OnReload();
    |
    v
IOptionsMonitor<SamplerOptions> / IOptionsMonitor<SdkLimitOptions>   <-- lives in SDK core
    |   M.E.Options recomputes; IValidateOptions<T> rejects invalid values
    |
    v
OnChange subscriber in SDK component                                  <-- lives in SDK core
    |   Disposal guard -> name guard -> try { apply } catch { log, retain previous }
    |
    v
Running SDK components reflect new behaviour on next activity/log/metric
```

### H.4 OpAMP Considerations

OpAMP is the most likely management-plane protocol for telemetry policies in
production deployments, but it is **not special from the SDK's perspective** --
it is one concrete `IConfigurationProvider` implementation among several. The
OpAMP package translates OpAMP effective configuration messages into
`IConfiguration` key/value pairs and calls `OnReload()`. The SDK sees only
`IOptionsMonitor<T>` change notifications, with no awareness of the underlying
transport.

Key design constraints for the OpAMP package:

- The OpAMP client's network receive callback must not block. The
  `IConfigurationProvider` adapter should accept the new values and return
  immediately; `OnReload()` dispatches asynchronously via the options
  infrastructure.
- The OpAMP package should expose builder registration (e.g.,
  `AddOpAmpPolicySource(Action<OpAmpPolicyOptions>)`) that wires up the
  `IConfigurationSource` with correct priority ordering (highest - see [Risk
  3.5](configuration-analysis-risks.md#35-iconfigurationprovider-priority-ordering-determinism)).
- Consumers who want their own OpAMP-based configuration source (not limited to
  telemetry policies) are free to implement their own `IConfigurationProvider`
  against the OpAMP client - the SDK's abstractions do not prevent this.

--> [Deep Dive
C](configuration-analysis-deep-dives.md#c-otlp-exporter-snapshot-architecture-and-reload-path)
covers OTLP-specific reload. [Deep Dive
D](configuration-analysis-deep-dives.md#d-sampler-reloadability-design) covers
sampler reload via `ReloadableSampler`.

---

## I. Configuration SDK Spec Alignment

This section covers implementation details for aligning with the [Configuration
SDK
specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/configuration/sdk.md).
The open design questions (in-memory model, Parse/Create operations,
ConfigProvider) are discussed in [SS5.1 of the executive
summary](configuration-analysis.md#51-spec-alignment---configuration-sdk-operations).
This deep dive focuses on concrete implementation requirements.

### I.1 YAML Environment Variable Substitution

The spec defines a `${VAR:-default}` substitution syntax for YAML configuration
files with detailed rules that differ from .NET's `IConfiguration` environment
variable provider. The substitution must happen during YAML parsing, before
values enter the `IConfiguration` tree.

#### I.1.1 Spec Requirements Summary

The substitution rules (from the [data model
spec](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/configuration/data-model.md#environment-variable-substitution)):

- `${VAR}` - substitute the value of environment variable `VAR`
- `${VAR:-fallback}` - substitute `VAR`, or `fallback` if `VAR` is undefined,
  empty, or null
- `$$` - escape sequence producing a literal `$`; the resolved `$` is not
  considered for further substitution matching
- Substitution applies only to scalar values, not mapping keys
- Substitution is not recursive - the result of one substitution is not
  scanned for further `${...}` references
- YAML structure injection via env vars is prohibited - substituted values
  are always scalars
- Node types are interpreted after substitution (e.g. `${BOOL_VALUE}` where
  `BOOL_VALUE=true` resolves to YAML boolean `true`, not string `"true"`)
- Invalid substitution references (e.g. `${VAR:?error}`) must produce a parse
  error with no partial results
- The optional `env:` prefix is supported: `${env:VAR}` is equivalent to
  `${VAR}`

#### I.1.2 Implementation Approach

The substitution should be implemented within the custom YAML
`IConfigurationProvider`. The processing order is:

```text
1. Read YAML file from disk
2. Perform ${VAR:-default} substitution on all scalar values
3. Parse substituted YAML into IConfiguration key/value pairs
4. Expose via standard IConfigurationProvider interface
```

Step 2 processes the raw YAML text (or the parsed YAML DOM scalar nodes)
before projecting into `IConfiguration`. This ensures type coercion happens
after substitution, matching the spec requirement.

#### I.1.3 Interplay with IConfiguration Environment Variable Loading

The SDK currently uses a vendored `EnvironmentVariablesConfigurationProvider` to
make `OTEL_*` env vars available as `IConfiguration` keys. When `OTEL_CONFIG_FILE`
is active, these two env-var mechanisms overlap:

| Mechanism | When it runs | What it does |
| --- | --- | --- |
| YAML `${VAR}` substitution | YAML parse time | Replaces placeholders in YAML scalar values with env var values |
| `EnvironmentVariablesConfigurationProvider` | `IConfiguration` build time | Makes all `OTEL_*` env vars available as `IConfiguration` keys that can override options |

The spec states that when `OTEL_CONFIG_FILE` is set, the config file is the
authoritative source and environment variables should not override values
defined in the file (see [Risk
3.1](configuration-analysis-risks.md#31-otel_config_file-vs-iconfiguration-hierarchy---resolution-via-sdk-option)).
The YAML `${VAR}` substitution is the spec's mechanism for env-var-driven
values within declarative config.

**Recommended behaviour when `OTEL_CONFIG_FILE` is active:**

- The `EnvironmentVariablesConfigurationProvider` for OTel-specific keys should
  **not** be layered above the YAML provider in the `IConfiguration` source
  chain. This prevents env vars from silently overriding YAML-defined values.
- The YAML provider's `${VAR}` substitution is the only path for env vars to
  influence values defined in the YAML file.
- Env vars not represented in the YAML file (e.g. `OTEL_SERVICE_NAME` when
  `resource.attributes.service.name` is absent from the YAML) should still be
  available via the standard env var provider for properties that the YAML file
  does not define. The priority model should be: YAML-defined values (with
  `${VAR}` substitution already applied) take precedence over env var fallbacks
  for the same logical property.

**When `OTEL_CONFIG_FILE` is not set:** The current behaviour is unchanged.
The `EnvironmentVariablesConfigurationProvider` remains the primary source for
`OTEL_*` keys.

#### I.1.4 Testing Considerations

The substitution implementation needs test coverage for:

- Basic substitution (`${VAR}` with defined and undefined vars)
- Default values (`${VAR:-fallback}`)
- Escape sequences (`$$`, `$$$`, `$$$$`)
- Type preservation after substitution (bool, int, float, string)
- Structure injection prevention (env var containing newlines/colons)
- Non-recursive substitution (`${VAR}` where VAR's value contains `${...}`)
- Invalid substitution references producing parse errors
- The `env:` prefix variant

The spec provides a comprehensive examples table that can be translated
directly into test cases.

### I.2 Schema Validation Requirements

The spec requires two distinct validation layers, only one of which the current
design addresses.

#### I.2.1 Two-Layer Validation Model

| Layer | What it validates | When it runs | Current status |
| --- | --- | --- | --- |
| **Schema validation** | YAML structure conforms to the config data model: required fields present, types correct, no unknown keys, valid nesting | During Parse, before values enter `IConfiguration` | **Not addressed** |
| **Options validation** | Individual option values are semantically valid: port in range, endpoint is valid URI, sampler arg in `[0.0, 1.0]` | During `DelegatingOptionsFactory.Create`, via `IValidateOptions<T>` | Addressed in [Risk 1.1](configuration-analysis-risks.md#11-options-validation-is-completely-absent) (Step 0a) |

Both layers are needed. Schema validation catches structural problems early
(before any SDK component construction). Options validation catches semantic
problems that schema alone cannot express.

#### I.2.2 Schema Validation Implementation

The spec's configuration data model is defined as a [JSON
Schema](https://github.com/open-telemetry/opentelemetry-configuration). Schema
validation can be implemented by:

1. Deserializing the YAML file into a JSON-compatible DOM
2. Validating against the published JSON Schema (or a compiled representation)
3. Reporting all violations with file location context (line/column) before
   any `IConfiguration` projection

For Option C (typed model for Parse, `IConfiguration` for Create - see
[SS5.1.1](configuration-analysis.md#511-in-memory-configuration-model)), schema
validation falls out naturally from typed deserialization: if the YAML cannot
be deserialized into the typed `OpenTelemetryConfiguration` model, Parse returns
an error.

For Option A (`IConfiguration` only), schema validation must be a separate step
before or during the YAML-to-`IConfiguration` projection.

#### I.2.3 Unknown Key Handling

The spec's JSON Schema defines the valid set of keys. The YAML provider should
detect and report unknown keys rather than silently ignoring them, since
unknown keys almost always indicate typos:

```yaml
tracer_provider:
  processors:
    - batch:
        exporter:
          otlp:
            endpoint: https://collector.example.com:4317  # typo: "endpoint"
```

Without schema validation, `endpoint` silently becomes an unused
`IConfiguration` key and the endpoint defaults to `localhost:4317`.

### I.3 `file_format` Versioning

The spec requires a `file_format` field at the root of every configuration
file:

```yaml
file_format: "0.4"
sdk:
  traces:
    sampler:
      type: parentbased_traceidratio
      arg: 0.5
```

#### I.3.1 Requirements

- The YAML `IConfigurationProvider` must read `file_format` before processing
  the rest of the file
- If `file_format` is missing, Parse must return an error
- If `file_format` specifies a version the SDK does not support, Parse must
  return an error with a clear message identifying supported versions
- The SDK should document which `file_format` versions it supports per release

#### I.3.2 Version Compatibility Policy

The `file_format` version follows the `opentelemetry-configuration` repository's
[versioning
policy](https://github.com/open-telemetry/opentelemetry-configuration/blob/main/VERSIONING.md).
The SDK should support:

- The current stable `file_format` version
- Optionally, a limited set of prior versions with documented deprecation
  timeline

The version check should happen as the first step in Parse, before env var
substitution or schema validation, so that version-incompatible files fail
immediately with an actionable error.
