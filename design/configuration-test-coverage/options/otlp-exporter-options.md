# OtlpExporterOptions - Configuration Test Coverage

Per-options-class file. Loads against
[`configuration-test-coverage.md`](../../configuration-test-coverage.md) and
the relevant rows of
[`existing-tests.md`](../existing-tests.md). Opinion lives in Sections 2
and 3; Section 1 is inventory only.

## Source citations

- Type declaration and default constants -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpExporterOptions.cs:25-40`
  (`DefaultGrpcEndpoint`, `DefaultHttpEndpoint`,
  `DefaultOtlpExportProtocol`, `DefaultHeaders`).
- Public parameterless constructor (DI-unfriendly by design; builds its own
  env-var-backed `IConfiguration`) -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpExporterOptions.cs:50-53`.
- Internal constructor that takes `OtlpExporterOptionsConfigurationType` -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpExporterOptions.cs:55-62`.
- Internal constructor that takes `IConfiguration`, configuration type,
  default batch options -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpExporterOptions.cs:64-92`.
- Property declarations -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpExporterOptions.cs`:
  - `Endpoint` getter/setter - lines 95-118 (getter falls back by
    `Protocol`; setter throws on null and resets
    `AppendSignalPathToEndpoint`).
  - `Headers` - line 121.
  - `TimeoutMilliseconds` (default 10000) - lines 124-128.
  - `Protocol` - lines 131-135.
  - `UserAgentProductIdentifier` - line 141.
  - `ExportProcessorType` - line 147.
  - `BatchExportProcessorOptions` - line 153.
  - `HttpClientFactory` - lines 156-165 (setter throws on null).
  - `StandardHeaders` (internal) - lines 167-170.
  - `AppendSignalPathToEndpoint` (internal) - line 180.
  - `MtlsOptions` (internal, `NET` only) - line 183.
  - `HasData` (internal) - lines 186-190.
- Env-var reads keyed by configuration type
  (`Default`/`Logs`/`Metrics`/`Traces`) -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpExporterOptions.cs:253-310`
  via `ApplyConfigurationUsingSpecificationEnvVars` at lines 200-232.
- Cascade/defaults application (used when signal-specific instance falls
  back to the default instance) -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpExporterOptions.cs:234-251`.
- mTLS env var reads (`NET` only) -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpExporterOptions.cs:313-337`.
- Spec env-var name constants -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/OtlpSpecConfigDefinitions.cs:14-39`.
- Factory registration for `AddOtlpExporter` pathway -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/OtlpServiceCollectionExtensions.cs:54`
  (`services.RegisterOptionsFactory(OtlpExporterOptions.CreateOtlpExporterOptions);`).
- `CreateOtlpExporterOptions` internal factory (used by that
  registration) -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpExporterOptions.cs:192-198`.
- Four named `OtlpExporterOptions` instances inside
  `OtlpExporterBuilderOptions` used by the `UseOtlpExporter` pathway -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Builder/OtlpExporterBuilderOptions.cs:21-24`
  (fields) and `:46-52` (constructor).
  - `DefaultOptionsInstance` uses
    `OtlpExporterOptionsConfigurationType.Default`.
  - `LoggingOptionsInstance` uses
    `OtlpExporterOptionsConfigurationType.Logs`.
  - `MetricsOptionsInstance` uses
    `OtlpExporterOptionsConfigurationType.Metrics`.
  - `TracingOptionsInstance` uses
    `OtlpExporterOptionsConfigurationType.Traces`.
- `Configure*` delegates on the builder that target each of the four
  instances -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Builder/OtlpExporterBuilder.cs`:
  `ConfigureDefaultExporterOptions` 49-58,
  `ConfigureLoggingExporterOptions` 60-69,
  `ConfigureMetricsExporterOptions` 80-89,
  `ConfigureTracingExporterOptions` 100-109.
- `IConfiguration` binding for the four instances (reflection-based;
  flagged as AOT risk by Issue 4) -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Builder/OtlpExporterBuilder.cs:120-163`.
- Options-factory registration for `OtlpExporterBuilderOptions` -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Builder/OtlpExporterBuilder.cs:176-191`.
- Cascade application at pipeline build time
  (`signal.ApplyDefaults(default)`) -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Builder/OtlpExporterBuilder.cs:200`
  (logs), `:219` (metrics), `:234` (traces).

### Direct consumer sites

Consumers that read `OtlpExporterOptions` properties (pins which
behaviours are only observable at the consumer):

- `OtlpExporterOptionsExtensions.GetHeaders` -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpExporterOptionsExtensions.cs:25-50`
  (reads `options.Headers`, merges with
  `OtlpExporterOptions.StandardHeaders`).
- `OtlpExporterOptionsExtensions.GetExportTransmissionHandler` -
  `OtlpExporterOptionsExtensions.cs:71-95`
  (reads `TimeoutMilliseconds`).
- `OtlpExporterOptionsExtensions.GetExportClient` -
  `OtlpExporterOptionsExtensions.cs:101-125`
  (reads `HttpClientFactory` and `Protocol`; throws on unsupported
  `Protocol`).
- `OtlpExporterOptionsExtensions.TryEnableIHttpClientFactoryIntegration` -
  `OtlpExporterOptionsExtensions.cs:130-160`
  (re-wraps `HttpClientFactory` when `Protocol == HttpProtobuf` and the
  caller has not overridden `HttpClientFactory`).
- `OtlpExportClient` constructor -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/ExportClient/OtlpExportClient.cs:39-49`
  (reads `Endpoint` + `AppendSignalPathToEndpoint`; computes transport
  `Endpoint` and header dictionary).

---

## 1. Existing coverage

Pulled from
[`existing-tests.md`](../existing-tests.md). Inventory only.

`File:method` is abbreviated to the test-method name where the file is
unambiguous. Projects:

- `OTPT` = `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/`.

### 1.1 `OtlpExporterOptionsTests.cs` (OTPT)

| File:method | Scenario summary | Observation mechanism | Env-var isolation |
| --- | --- | --- | --- |
| `OtlpExporterOptionsTests.OtlpExporterOptions_Defaults` | Default endpoint, protocol, timeout, headers with no env vars | DirectProperty | Class-level `IDisposable` snapshot/restore + `[Collection]` |
| `OtlpExporterOptionsTests.OtlpExporterOptions_DefaultsForHttpProtobuf` | Defaults when `Protocol` is set to `HttpProtobuf` | DirectProperty | Class-level snapshot/restore |
| `OtlpExporterOptionsTests.OtlpExporterOptions_EnvironmentVariableOverride` | Env var overrides for all four signal types (Theory) | DirectProperty after ctor | Class-level snapshot/restore |
| `OtlpExporterOptionsTests.OtlpExporterOptions_UsingIConfiguration` | `IConfiguration` (appsettings-shaped) init for all four signal types (Theory) | DirectProperty after ctor | Class-level snapshot/restore |
| `OtlpExporterOptionsTests.OtlpExporterOptions_InvalidEnvironmentVariableOverride` | Invalid env var values rejected (endpoint, timeout, protocol) | DirectProperty + fall-back to default | Class-level snapshot/restore |
| `OtlpExporterOptionsTests.OtlpExporterOptions_SetterOverridesEnvironmentVariable` | Programmatic setter beats env/config | DirectProperty | Class-level snapshot/restore |
| `OtlpExporterOptionsTests.OtlpExporterOptions_EndpointGetterUsesProtocolWhenNull` | `Endpoint` getter returns gRPC vs HTTP default by `Protocol` | DirectProperty | Class-level snapshot/restore |
| `OtlpExporterOptionsTests.OtlpExporterOptions_EndpointThrowsWhenSetToNull` | `Endpoint = null` throws | DirectProperty (exception) | Class-level snapshot/restore |
| `OtlpExporterOptionsTests.OtlpExporterOptions_SettingEndpointToNullResetsAppendSignalPathToEndpoint` | Endpoint null assignment behaviour | DirectProperty | Class-level snapshot/restore |
| `OtlpExporterOptionsTests.OtlpExporterOptions_HttpClientFactoryThrowsWhenSetToNull` | `HttpClientFactory = null` throws | DirectProperty (exception) | Class-level snapshot/restore |
| `OtlpExporterOptionsTests.OtlpExporterOptions_ApplyDefaultsTest` | `ApplyDefaults` cascade between two instances | DirectProperty | Class-level snapshot/restore |
| `OtlpExporterOptionsTests.OtlpExporterOptions_MtlsEnvironmentVariables` | `OTEL_EXPORTER_OTLP_CERTIFICATE` env var | DirectProperty on `MtlsOptions` | Class-level snapshot/restore |
| `OtlpExporterOptionsTests.OtlpExporterOptions_MtlsEnvironmentVariables_ClientCertificate` | `CLIENT_CERTIFICATE` + `CLIENT_KEY` env vars | DirectProperty on `MtlsOptions` | Class-level snapshot/restore |
| `OtlpExporterOptionsTests.OtlpExporterOptions_MtlsEnvironmentVariables_AllCertificates` | All three mTLS env vars | DirectProperty on `MtlsOptions` | Class-level snapshot/restore |
| `OtlpExporterOptionsTests.OtlpExporterOptions_MtlsEnvironmentVariables_NoEnvironmentVariables` | No mTLS when no env vars set | DirectProperty on `MtlsOptions` | Class-level snapshot/restore |
| `OtlpExporterOptionsTests.OtlpExporterOptions_MtlsEnvironmentVariables_UsingIConfiguration` | mTLS options via `IConfiguration` | DirectProperty on `MtlsOptions` | Class-level snapshot/restore |
| `OtlpExporterOptionsTests.UserAgentProductIdentifier_Default_IsEmpty` | Default `UserAgentProductIdentifier` is empty | DirectProperty | Class-level snapshot/restore |
| `OtlpExporterOptionsTests.UserAgentProductIdentifier_DefaultUserAgent_ContainsExporterInfo` | Default `User-Agent` contains `OTel-OTLP-Exporter-Dotnet` | DirectProperty on `StandardHeaders` | Class-level snapshot/restore |
| `OtlpExporterOptionsTests.UserAgentProductIdentifier_WithProductIdentifier_IsPrepended` | Custom identifier prepended to User-Agent | DirectProperty on `StandardHeaders` | Class-level snapshot/restore |
| `OtlpExporterOptionsTests.UserAgentProductIdentifier_UpdatesStandardHeaders` | Setter updates `StandardHeaders` | DirectProperty | Class-level snapshot/restore |
| `OtlpExporterOptionsTests.UserAgentProductIdentifier_Rfc7231Compliance_SpaceSeparatedTokens` | User-Agent RFC-7231 token format | DirectProperty on `StandardHeaders` | Class-level snapshot/restore |
| `OtlpExporterOptionsTests.UserAgentProductIdentifier_EmptyOrWhitespace_UsesDefaultUserAgent` | Empty/whitespace identifier falls back (Theory) | DirectProperty on `StandardHeaders` | Class-level snapshot/restore |
| `OtlpExporterOptionsTests.UserAgentProductIdentifier_MultipleProducts_CorrectFormat` | Multiple product tokens | DirectProperty on `StandardHeaders` | Class-level snapshot/restore |

### 1.2 `OtlpExporterOptionsExtensionsTests.cs` (OTPT)

| File:method | Scenario summary | Observation mechanism | Env-var isolation |
| --- | --- | --- | --- |
| `OtlpExporterOptionsExtensionsTests.GetHeaders_NoOptionHeaders_ReturnsStandardHeaders` | Empty/null `Headers` -> standard headers only (Theory) | DirectProperty (via extension) | Not env-var dependent |
| `OtlpExporterOptionsExtensionsTests.GetHeaders_InvalidOptionHeaders_ThrowsArgumentException` | Malformed `Headers` throws (Theory) | Exception | Not env-var dependent |
| `OtlpExporterOptionsExtensionsTests.GetHeaders_ValidAndUrlEncodedHeaders_ReturnsCorrectHeaders` | URL-encoded header value parsing (Theory) | DirectProperty (via extension) | Not env-var dependent |
| `OtlpExporterOptionsExtensionsTests.GetTraceExportClient_SupportedProtocol_ReturnsCorrectExportClient` | `Protocol` -> export-client type mapping (Theory) | Behavioural side-effect (type check) | Not env-var dependent |
| `OtlpExporterOptionsExtensionsTests.GetTraceExportClient_UnsupportedProtocol_Throws` | Invalid `Protocol` enum value throws | Exception | Not env-var dependent |
| `OtlpExporterOptionsExtensionsTests.AppendPathIfNotPresent_TracesPath_AppendsCorrectly` | Signal-path appending to `Endpoint` (Theory) | DirectProperty | Not env-var dependent |

### 1.3 `OtlpExporterHelperExtensionsTests.cs` (OTPT)

Covers the `AddOtlpExporter` pathway.

| File:method | Scenario summary | Observation mechanism | Env-var isolation |
| --- | --- | --- | --- |
| `OtlpExporterHelperExtensionsTests.OtlpExporter_Throws_OnGrpcWithDefaultFactory_ForTracing` | `AddOtlpExporter` + `Grpc` without custom `HttpClientFactory` throws on `NETFRAMEWORK`/`NETSTANDARD2_0` | Exception | Not env-var dependent |
| `OtlpExporterHelperExtensionsTests.OtlpExporter_Throws_OnGrpcWithDefaultFactory_ForMetrics` | Same for metrics | Exception | Not env-var dependent |
| `OtlpExporterHelperExtensionsTests.OtlpExporter_Throws_OnGrpcWithDefaultFactory_ForLogging` | Same for logging | Exception | Not env-var dependent |
| `OtlpExporterHelperExtensionsTests.OtlpExporter_DoesNotThrow_WhenCustomHttpClientFactoryIsSet_ForTraces` | Custom `HttpClientFactory` avoids the exception (traces) | Behavioural side-effect | Not env-var dependent |
| `OtlpExporterHelperExtensionsTests.OtlpExporter_DoesNotThrow_WhenCustomHttpClientFactoryIsSet_ForMetrics` | Same for metrics | Behavioural side-effect | Not env-var dependent |
| `OtlpExporterHelperExtensionsTests.OtlpExporter_DoesNotThrow_WhenCustomHttpClientFactoryIsSet_ForLogging` | Same for logging | Behavioural side-effect | Not env-var dependent |

### 1.4 Signal-specific exporter tests touching `OtlpExporterOptions` (OTPT)

| File:method | Scenario summary | Observation mechanism | Env-var isolation |
| --- | --- | --- | --- |
| `OtlpTraceExporterTests.AddOtlpTraceExporterNamedOptionsSupported` | Named-options support via `AddOtlpExporter(name, configure)` | DI `IOptionsMonitor<OtlpExporterOptions>` | `[Collection]` attribute |
| `OtlpTraceExporterTests.OtlpExporter_BadArgs` | Bad constructor args validated | Exception | `[Collection]` attribute |
| `OtlpTraceExporterTests.UserHttpFactoryCalled` | User `HttpClientFactory` invoked | Mock factory | `[Collection]` attribute |
| `OtlpTraceExporterTests.ServiceProviderHttpClientFactoryInvoked` | `IHttpClientFactory` from DI invoked | Mock factory | `[Collection]` attribute |
| `OtlpTraceExporterTests.NonnamedOptionsMutateSharedInstanceTest` | Unnamed options share one instance | DI (Microsoft.Extensions.Options) | `[Collection]` attribute |
| `OtlpTraceExporterTests.NamedOptionsMutateSeparateInstancesTest` | Named options yield separate instances | DI (Microsoft.Extensions.Options) | `[Collection]` attribute |
| `OtlpLogExporterTests.AddOtlpExporterWithNamedOptions` | Named-options support (logging pathway) | DI `IOptionsMonitor<OtlpExporterOptions>` | `[Collection]` attribute |
| `OtlpLogExporterTests.UserHttpFactoryCalledWhenUsingHttpProtobuf` | User `HttpClientFactory` invoked (HttpProtobuf) | Mock factory | `[Collection]` attribute |
| `OtlpMetricsExporterTests.TestAddOtlpExporter_NamedOptions` | Named-options support (metrics pathway) | DI `IOptionsMonitor<OtlpExporterOptions>` | `[Collection]` attribute |
| `OtlpMetricsExporterTests.UserHttpFactoryCalled` | User `HttpClientFactory` invoked | Mock factory | `[Collection]` attribute |
| `OtlpMetricsExporterTests.ServiceProviderHttpClientFactoryInvoked` | `IHttpClientFactory` from DI invoked | Mock factory | `[Collection]` attribute |

### 1.5 `UseOtlpExporterExtensionTests.cs` (OTPT) - `UseOtlpExporter` pathway

`UseOtlpExporter` exercises the four named `OtlpExporterOptions` instances
through `OtlpExporterBuilderOptions`. Rows listed here only when they
pin an `OtlpExporterOptions`-observable effect.

| File:method | Scenario summary | Observation mechanism | Env-var isolation |
| --- | --- | --- | --- |
| `UseOtlpExporterExtensionTests.UseOtlpExporterDefaultTest` | Defaults for all four instances after `UseOtlpExporter()` | DI `IOptionsMonitor<OtlpExporterBuilderOptions>` | `[Collection]` attribute |
| `UseOtlpExporterExtensionTests.UseOtlpExporterSetEndpointAndProtocolTest` | Endpoint + Protocol overload sets all four instances (Theory) | DI `IOptionsMonitor<OtlpExporterBuilderOptions>` | `[Collection]` attribute |
| `UseOtlpExporterExtensionTests.UseOtlpExporterConfigureTest` | `ConfigureDefaultExporterOptions` / `ConfigureLoggingExporterOptions` / `ConfigureMetricsExporterOptions` / `ConfigureTracingExporterOptions` delegates, named + unnamed (Theory) | DI `IOptionsMonitor<OtlpExporterBuilderOptions>.Get(name)` | `[Collection]` attribute |
| `UseOtlpExporterExtensionTests.UseOtlpExporterConfigurationTest` | `UseOtlpExporter(IConfiguration)` binds `DefaultOptions`/`LoggingOptions`/`MetricsOptions`/`TracingOptions` sections for named + unnamed (Theory) | DI `IOptionsMonitor<OtlpExporterBuilderOptions>` | `[Collection]` attribute |
| `UseOtlpExporterExtensionTests.UseOtlpExporterRespectsSpecEnvVarsTest` | Env vars applied to each of the four instances with signal-specific precedence | DI `IOptionsMonitor<OtlpExporterBuilderOptions>` | `[Collection]` attribute |
| `UseOtlpExporterExtensionTests.UseOtlpExporterRespectsSpecEnvVarsSetUsingIConfigurationTest` | Same as above via `IConfiguration` instead of env vars | DI `IOptionsMonitor<OtlpExporterBuilderOptions>` | `[Collection]` attribute |

---

## 2. Scenario checklist and gap analysis

Status column values: **covered**, **partial**, **missing**. "Currently
tested by" cites tests from Section 1 or dashes for none.

### 2.1 Constructor env-var reads (per property, per configuration type)

Default configuration type reads
`OTEL_EXPORTER_OTLP_{ENDPOINT|HEADERS|TIMEOUT|PROTOCOL}`. Logs/Metrics/Traces
read the signal-specific equivalents.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `Endpoint` from `OTEL_EXPORTER_OTLP_ENDPOINT` (Default type) | `OtlpExporterOptions_EnvironmentVariableOverride` (Theory row) | Parsed to `Uri`; `AppendSignalPathToEndpoint = true` | covered |
| `Endpoint` from signal-specific env var (Logs/Metrics/Traces types) | `OtlpExporterOptions_EnvironmentVariableOverride`, `UseOtlpExporterRespectsSpecEnvVarsTest` | Parsed; `AppendSignalPathToEndpoint = false` | covered |
| `Protocol` from `OTEL_EXPORTER_OTLP_PROTOCOL` + signal-specific | `OtlpExporterOptions_EnvironmentVariableOverride` | Parsed via `OtlpExportProtocolParser.TryParse` | covered |
| `Headers` from `OTEL_EXPORTER_OTLP_HEADERS` + signal-specific | `OtlpExporterOptions_EnvironmentVariableOverride` | Raw string assignment | covered |
| `TimeoutMilliseconds` from `OTEL_EXPORTER_OTLP_TIMEOUT` + signal-specific | `OtlpExporterOptions_EnvironmentVariableOverride` | Parsed int | covered |
| `UserAgentProductIdentifier` from env var | - | Not bound by env var (no spec env var defined) | n/a (no env-var binding) |
| `ExportProcessorType` from env var | - | Not bound by env var | n/a |
| `BatchExportProcessorOptions` from env var | - | Not bound by env var directly (separate class has its own env vars) | n/a |
| `HttpClientFactory` from env var | - | Not bound by env var | n/a |
| `MtlsOptions.CaCertificatePath` from `OTEL_EXPORTER_OTLP_CERTIFICATE` (`NET` only) | `OtlpExporterOptions_MtlsEnvironmentVariables`, `OtlpExporterOptions_MtlsEnvironmentVariables_AllCertificates` | Stored into `MtlsOptions.CaCertificatePath` | covered |
| `MtlsOptions.ClientCertificatePath` from `OTEL_EXPORTER_OTLP_CLIENT_CERTIFICATE` | `OtlpExporterOptions_MtlsEnvironmentVariables_ClientCertificate`, `_AllCertificates` | Stored | covered |
| `MtlsOptions.ClientKeyPath` from `OTEL_EXPORTER_OTLP_CLIENT_KEY` | `OtlpExporterOptions_MtlsEnvironmentVariables_ClientCertificate`, `_AllCertificates` | Stored | covered |

Notes:

- The env-var read happens inside the `OtlpExporterOptions` constructor;
  the public parameterless constructor builds its own
  `ConfigurationBuilder().AddEnvironmentVariables().Build()` so the
  env vars are effectively read at construction time and never again.
- `ApplyConfigurationUsingSpecificationEnvVars` is only called once per
  instance; there is no reload path today.

### 2.2 Priority order

The target order for this class (where applicable) is: programmatic
`Configure<T>` > `appsettings.json` (via `IConfiguration`) > env var >
factory default > type default.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Programmatic setter beats env var | `OtlpExporterOptions_SetterOverridesEnvironmentVariable` | Setter wins | covered |
| Programmatic setter beats `IConfiguration` | `OtlpExporterOptions_SetterOverridesEnvironmentVariable` | Setter wins | covered |
| `Configure<OtlpExporterOptions>` delegate beats env var (DI pathway via `AddOtlpExporter`) | - | Unverified: `CreateOtlpExporterOptions` factory builds an env-var-backed options object, then Microsoft.Extensions.Options applies `Configure<T>` after | missing |
| `Configure<OtlpExporterOptions>` beats `appsettings.json` (standalone `AddOtlpExporter`) | - | Unverified; `AddOtlpExporter` does not wire `IConfiguration` directly into `OtlpExporterOptions` except via the env-var-backed one inside the factory | missing |
| `IConfiguration` (appsettings-shaped) beats env var (Default type) | `OtlpExporterOptions_UsingIConfiguration` | Per-test pattern: env var set first, then `IConfiguration` with same key wins inside the constructor (single-source read) | partial (not a layered test; ends before the "Configure<T>" step) |
| `UseOtlpExporter(IConfiguration)` binds the four named instances from appsettings | `UseOtlpExporterConfigurationTest` | Reflection-based binding via `services.Configure<OtlpExporterBuilderOptions>` | covered (but see Issue 4 AOT risk) |
| Factory default (constant in source) applied when neither env var, `IConfiguration`, nor `Configure<T>` touches the property | `OtlpExporterOptions_Defaults`, `OtlpExporterOptions_DefaultsForHttpProtobuf` | `Endpoint` -> protocol-dependent default; `Protocol` -> TFM-dependent default; `TimeoutMilliseconds` -> 10000; `Headers` -> null; `HttpClientFactory` -> `DefaultHttpClientFactory` | covered for property defaults (no DI in test) |
| Type default observed via DI (`AddOtlpExporter`) | - | Not directly tested; covered indirectly via DI-using named-options tests but they do not assert defaults | missing |
| Type default observed via `UseOtlpExporter` | `UseOtlpExporterDefaultTest` | All four instances carry per-signal defaults | covered |

### 2.3 Default-state baseline

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| All `OtlpExporterOptions` properties at their defaults (no env vars, no config) | `OtlpExporterOptions_Defaults`, `OtlpExporterOptions_DefaultsForHttpProtobuf` | Property-by-property checks for `Endpoint`, `Protocol`, `TimeoutMilliseconds`, `Headers`, `HttpClientFactory` non-null | covered at property level |
| Stable snapshot of the default shape (including `ExportProcessorType`, `BatchExportProcessorOptions`, `UserAgentProductIdentifier`, `AppendSignalPathToEndpoint`, `HasData`, `MtlsOptions`) | - | Not snapshotted | missing (candidate for snapshot-library pilot) |

### 2.4 Named options

`OtlpExporterOptions` is DI-registered via
`OtlpServiceCollectionExtensions.AddOtlpExporterSharedServices`
(line 50) which calls
`services.RegisterOptionsFactory(OtlpExporterOptions.CreateOtlpExporterOptions)`
at line 54. The `UseOtlpExporter` pathway additionally constructs four
named instances inside `OtlpExporterBuilderOptions` keyed by the builder
`name` (`"otlp"` by default).

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `IOptionsMonitor<OtlpExporterOptions>.Get(Options.DefaultName)` returns an instance with env-var-applied defaults | `NonnamedOptionsMutateSharedInstanceTest` (asserts instance-sharing), `OtlpExporterOptions_Defaults` (asserts values but outside DI) | Factory returns new instance with `Default` configuration type; `IOptionsMonitor` caches per name | partial (no test asserts both identity *and* values resolved via DI) |
| Named `IOptionsMonitor<OtlpExporterOptions>.Get("foo")` returns a distinct instance | `NamedOptionsMutateSeparateInstancesTest`, `AddOtlpTraceExporterNamedOptionsSupported`, `AddOtlpExporterWithNamedOptions`, `TestAddOtlpExporter_NamedOptions` | Distinct cached instance per name; env-var defaults re-applied in each | covered |
| Named options cascade for signal-specific env vars: named `"foo"` receives `OTEL_EXPORTER_OTLP_ENDPOINT` (there is no `OTEL_EXPORTER_OTLP_foo_ENDPOINT` concept; name is an MEO name, not a signal) | - | The factory always uses `Default` configuration type regardless of the name (see `CreateOtlpExporterOptions`) | missing |
| `UseOtlpExporter` resolves the four named instances (`DefaultOptionsInstance`, `LoggingOptionsInstance`, `MetricsOptionsInstance`, `TracingOptionsInstance`) with the correct `OtlpExporterOptionsConfigurationType` applied | `UseOtlpExporterRespectsSpecEnvVarsTest`, `UseOtlpExporterDefaultTest`, `UseOtlpExporterSetEndpointAndProtocolTest` | Four instances constructed with `Default`/`Logs`/`Metrics`/`Traces` types respectively | covered |
| `UseOtlpExporter` cascade: signal-specific instance inherits from `DefaultOptionsInstance` when its own env vars are unset | `UseOtlpExporterRespectsSpecEnvVarsTest` (Theory exercises both, per signal), via `ApplyDefaults` at builder pipeline time | `signal.ApplyDefaults(default)` runs during `ConfigureOpenTelemetry{Logger,Meter,Tracer}Provider` | partial (covered at the options level but not end-to-end observed at the consumer for every property) |
| Named-options fallback to generic `OTEL_EXPORTER_OTLP_*` when signal-specific env var is absent (precedence ordering) | `OtlpExporterOptions_EnvironmentVariableOverride` (Theory) | Signal-specific env var wins; absent -> default instance inherits at `ApplyDefaults` time (not per-property fallback inside one options instance) | partial (the fallback is at the instance level, not the property level; no test pins that behaviour for each property) |
| Interaction: `ConfigureDefaultExporterOptions` + signal-specific env var present - does signal instance or Default `Configure<T>` win? | - | Unverified by test (`ApplyDefaults` only fills unset fields, so env-var-set signal property wins; but `Configure<T>` on Default runs after signal instance construction) | missing |

### 2.5 Invalid-input characterisation

Each property asked: what does the code do today when input is malformed,
null, out of range, or the wrong type? Pin today's behaviour so Issue 1
validation work has a visible delta.

| Property | Malformed input source | Current behaviour | Currently tested by | Status |
| --- | --- | --- | --- | --- |
| `Endpoint` | Env var not a URI | `TryGetUriValue` returns false; default kept. Eventually logs via `OpenTelemetryProtocolExporterEventSource.Log` | `OtlpExporterOptions_InvalidEnvironmentVariableOverride` | covered |
| `Endpoint` | Programmatic `null` | `Guard.ThrowIfNull` throws `ArgumentNullException` | `OtlpExporterOptions_EndpointThrowsWhenSetToNull` | covered |
| `Endpoint` | Programmatic not-absolute `Uri` | No validation; stored as-is | - | missing (silent accept) |
| `Endpoint` | `appsettings.json` malformed URI string | Same as env var: `TryGetUriValue` rejects, default kept | - | missing (no dedicated test) |
| `Protocol` | Env var unknown string | `OtlpExportProtocolParser.TryParse` returns false; default kept; logs | `OtlpExporterOptions_InvalidEnvironmentVariableOverride` | covered |
| `Protocol` | Programmatic unknown enum value | Stored as-is; `GetExportClient` throws `NotSupportedException` at consumer | `GetTraceExportClient_UnsupportedProtocol_Throws` | covered (at consumer) |
| `Headers` | Malformed string (e.g. no `=`) | Option accepts any string; `GetHeaders` throws `ArgumentException` at consumer | `GetHeaders_InvalidOptionHeaders_ThrowsArgumentException` | covered (at consumer) |
| `Headers` | URL-encoded values | `GetHeaders` decodes | `GetHeaders_ValidAndUrlEncodedHeaders_ReturnsCorrectHeaders` | covered |
| `TimeoutMilliseconds` | Env var non-numeric | Rejected; default kept; logs | `OtlpExporterOptions_InvalidEnvironmentVariableOverride` | covered |
| `TimeoutMilliseconds` | Programmatic negative | Stored as-is; `HttpClient.Timeout` will throw at construction | - | missing (silent accept at set time) |
| `TimeoutMilliseconds` | Programmatic zero | Stored as-is; `HttpClient.Timeout` accepts `Zero` but semantics fragile | - | missing |
| `HttpClientFactory` | Programmatic `null` | Throws `ArgumentNullException` | `OtlpExporterOptions_HttpClientFactoryThrowsWhenSetToNull` | covered |
| `HttpClientFactory` | Returns `null` at invocation | Throws `InvalidOperationException` at `GetExportClient` | - | missing (behaviour inferred from code) |
| `UserAgentProductIdentifier` | Whitespace/empty | Falls back to default User-Agent (Theory-asserted) | `UserAgentProductIdentifier_EmptyOrWhitespace_UsesDefaultUserAgent` | covered |
| `UserAgentProductIdentifier` | Non-RFC-7231 characters | No validation; passed through | - | missing |
| `ExportProcessorType` | Unknown enum value | Stored as-is; downstream throws or silently uses Batch | - | missing |
| `BatchExportProcessorOptions` | Programmatic `null` | Stored as `null`; downstream behaviour varies per consumer | - | missing |
| `MtlsOptions.*Path` | Non-existent file | File-not-found thrown at client-build time (see `OtlpCertificateManagerTests`) | Consumer-side tests in `OtlpCertificateManagerTests`, `OtlpSecureHttpClientFactoryTests` | covered (at consumer) |

All rows marked **missing** are expected to change under Issue 1 (add
`IValidateOptions<T>` + `ValidateOnStart` for all options classes). Tests
added here pin today's silent-accept behaviour so Issue 1 produces a
visible delta.

### 2.6 Reload no-op baseline

Today, `OtlpExporterOptions` does *not* participate in reload; the
factory reads env vars once at construction, and
`IOptionsMonitor<OtlpExporterOptions>` returns the cached instance after
first `Get`. `IOptionsMonitor.OnChange` subscriptions fire but built
exporter components do not re-consume.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `IOptionsMonitor<OtlpExporterOptions>.OnChange` on env-var change | - | No env-var change source is wired; `OnChange` only fires when an `IConfiguration` reload provider changes | missing |
| `IOptionsMonitor<OtlpExporterOptions>.OnChange` on `IConfigurationRoot.Reload()` -> built exporter's transport `Endpoint` unchanged | - | Not verified | missing |
| `IOptionsMonitor<OtlpExporterOptions>.OnChange` on reload -> built exporter's `TimeoutMilliseconds` unchanged | - | Not verified | missing |
| `IOptionsMonitor<OtlpExporterOptions>.OnChange` on reload -> built exporter's `Headers` unchanged | - | Not verified | missing |
| `IOptionsMonitor<OtlpExporterOptions>.OnChange` on reload -> built exporter's `Protocol` (and therefore transport client type) unchanged | - | Not verified | missing |

All five rows are expected to flip under Issue 17 (standard `OnChange`
subscriber pattern) and Issue 23 (`ReloadableOtlpExportClient` and
`IOptionsMonitor`-aware constructor).

### 2.7 Consumer-observed effects

Behaviours that are only visible at the consumer. Listed separately so
the mechanism mix (DI / InternalAccessor / Mock / Wire) is explicit.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `Endpoint` + `AppendSignalPathToEndpoint` -> transport URL at `OtlpExportClient.Endpoint` | `AppendPathIfNotPresent_TracesPath_AppendsCorrectly` (extension-level, not wired end-to-end) | `OtlpExportClient` ctor appends signal path when `AppendSignalPathToEndpoint` is `true` and `Protocol == HttpProtobuf` | partial (no wire-level test; no observation at built exporter) |
| `Headers` + `StandardHeaders` -> transport request headers | `GetHeaders_*` tests | `OtlpExportClient.Headers` dictionary merged from both sources | partial (no wire-level assertion) |
| `TimeoutMilliseconds` -> `HttpClient.Timeout` on the client returned by `DefaultHttpClientFactory` | - | `DefaultHttpClientFactory` sets `client.Timeout = TimeSpan.FromMilliseconds(this.TimeoutMilliseconds)` | missing |
| `TimeoutMilliseconds` -> `OtlpExporterTransmissionHandler.TimeoutMilliseconds` | Touched by `GetTransmissionHandler_InitializesCorrectHandlerExportClientAndTimeoutValue` (reflection) | Copied into handler via constructor | covered (reflection-based) |
| `HttpClientFactory` -> transport client instance | `UserHttpFactoryCalled`, `UserHttpFactoryCalledWhenUsingHttpProtobuf`, `ServiceProviderHttpClientFactoryInvoked` | Mock factory invoked | covered |
| `Protocol` -> export-client type | `GetTraceExportClient_SupportedProtocol_ReturnsCorrectExportClient` | `OtlpHttpExportClient` vs `OtlpGrpcExportClient` selected | covered (type-check level) |
| `UserAgentProductIdentifier` -> `User-Agent` request header | `UserAgent*` tests via `StandardHeaders` | Prepended to base User-Agent | covered (at options level; no wire assertion) |
| `ExportProcessorType` -> simple vs batch processor wiring | - | Consumer (`AddOtlpExporter` for tracing) picks the processor | missing |
| `BatchExportProcessorOptions` -> built `BatchActivityExportProcessor` timings | - | Consumer copies into processor | missing |
| `MtlsOptions` -> `OtlpSecureHttpClientFactory.CreateSecureHttpClient` path selection | Covered inside `OtlpSecureHttpClientFactoryTests` | Path taken when `MtlsOptions?.IsEnabled == true` | covered (per [otlp-mtls-options file], TBD) |

---

## 3. Recommendations

One bullet per gap. Each recommendation targets a reviewable PR unit.
Test name follows the dominant `Subject_Condition_Expected` convention
from the Session 0a naming survey. Target location is the existing test
file for the scenario; new files only where noted. Tier mapping per
entry-doc Section 3. Observation-mechanism labels match Section 2 of
the entry doc.

Rows are grouped by theme; within each theme ordering is from lowest
brittleness to highest.

### 3.1 DI-resolved defaults and `Configure<T>` priority

1. **`OtlpExporterOptions_Defaults_ObservedViaDi`** (new test in
   `OtlpTraceExporterTests.cs` next to the named-options tests).
   - Tier 2. Mechanism: DI
     (`IServiceProvider.GetRequiredService<IOptionsMonitor<OtlpExporterOptions>>()
     .Get(Options.DefaultName)`). Justifies the `AddOtlpExporter`
     pathway wires the env-var-backed factory as expected.
   - Guards Issues 1, 6. Risks pinned: `2.1`, `4.7`.
   - Code-comment hint: "Observation: DI - factory-produced defaults
     through `OtlpServiceCollectionExtensions.AddOtlpExporterSharedServices`".
   - Risk vs reward: low brittleness; high value because it closes the
     gap between DirectProperty defaults and what the DI pathway hands
     the exporter.
2. **`OtlpExporterOptions_ConfigureDelegate_BeatsEnvVar`** (new; same
   file).
   - Tier 2. Mechanism: DI + env-var set via fixture snapshot/restore.
     Calls `AddOtlpExporter(options => options.Endpoint = X)` then
     resolves and asserts. Justifies: observes priority order at the
     DI seam without reflection.
   - Guards Issues 1, 17.
   - Code-comment hint: "BASELINE: pins Configure<T> > env var order.
     Expected to remain true under Issue 17 (reload) but assertion is
     on steady state."
   - Risk vs reward: moderate setup for a load-bearing precedence row.
3. **`OtlpExporterOptions_ConfigureDelegate_BeatsAppsettings`** (new;
   same file). Mechanism and tier as above but drives state from an
   in-memory `IConfiguration`. Guards Issue 1. Pairs with the env-var
   variant; same code-comment template.

### 3.2 Named-options scenarios not currently covered

1. **`OtlpExporterOptions_CreateOtlpExporterOptions_IgnoresNamedParameter`**
   (new test in `OtlpExporterOptionsTests.cs`; or
   `OtlpExporterOptionsExtensionsTests.cs` if the author prefers to
   group factory tests).
   - Tier 1. Mechanism: InternalAccessor (the factory method is
     `internal static`). Calls
     `OtlpExporterOptions.CreateOtlpExporterOptions(sp, config, "foo")`
     twice with different names and asserts both instances received
     `OtlpExporterOptionsConfigurationType.Default` (observable via
     `HasData` + spec env var behaviour).
   - Guards Issues 1, 4.
   - Code-comment hint: "BASELINE: today the factory does not
     differentiate by name. This pins the behaviour so any future
     per-name signal wiring surfaces as a test delta."
   - Risk vs reward: low effort; closes a subtle gap.
2. **`UseOtlpExporter_ConfigureDefault_AppliesToSignalInstancesViaApplyDefaults`**
   (new; `UseOtlpExporterExtensionTests.cs`).
   - Tier 2. Mechanism: Mock (`DelegatingExporter` or equivalent; see
     Session 0a Sec.4.E for the existing helper) + DI resolution.
     Verifies that setting only `DefaultOptions.Endpoint` and leaving
     signal-specific env vars unset leads to the signal exporters using
     the default endpoint after `ApplyDefaults` runs.
   - Guards Issues 1, 14.
   - Code-comment hint: "Observation: Mock - pins `ApplyDefaults`
     cascade for `UseOtlpExporter`'s four instances."
   - Risk vs reward: moderate (requires the mock path) but pins the
     load-bearing four-instance cascade.
3. **`UseOtlpExporter_SignalEnvVar_WinsOverDefaultConfigureDelegate`**
   (new; same file).
   - Tier 2. Mechanism: DI + env-var fixture. Sets
     `OTEL_EXPORTER_OTLP_TRACES_ENDPOINT` then calls
     `ConfigureDefaultExporterOptions`. Asserts that the tracing
     instance still carries the env-var endpoint.
   - Guards Issues 1, 14.
   - Code-comment hint: "BASELINE: pins that signal-specific env var is
     sticky on the signal instance even when `Default` is then
     configured; `ApplyDefaults` only fills unset fields."
   - Risk vs reward: moderate effort; high value because the ordering
     is non-obvious from the source.

### 3.3 Invalid-input characterisation (guards Issue 1)

1. **`OtlpExporterOptions_Endpoint_RelativeUri_IsAcceptedSilently`**
   (new; `OtlpExporterOptionsTests.cs`). Tier 1. Mechanism:
   DirectProperty. Sets `options.Endpoint = new Uri("/x", UriKind.Relative)`
   (may throw at `Uri` construction on some TFMs; if so test is
   adjusted to assert the throw). Code-comment hint: "BASELINE: pins
   silent accept; expected to change under Issue 1 (validation)."
2. **`OtlpExporterOptions_TimeoutMilliseconds_Negative_IsAcceptedSilently`**
   and **`OtlpExporterOptions_TimeoutMilliseconds_Zero_IsAcceptedSilently`**
   (new; same file). Tier 1. Mechanism: DirectProperty. Guards Issue 1.
3. **`OtlpExporterOptions_UserAgentProductIdentifier_InvalidToken_IsAcceptedSilently`**
   (new; same file). Tier 1. Mechanism: DirectProperty on
   `StandardHeaders`. Pins that non-token characters pass through
   today. Guards Issue 1.
4. **`OtlpExporterOptions_ExportProcessorType_UnknownEnum_IsAcceptedSilently`**
   (new; same file). Tier 1. Mechanism: DirectProperty. Guards Issue 1.
5. **`OtlpExporterOptionsExtensions_HttpClientFactory_ReturnsNull_ThrowsInvalidOperationException`**
   (new; `OtlpExporterOptionsExtensionsTests.cs`). Tier 1. Mechanism:
   Exception. Pins the consumer-side throw that today is the only
   guard. Guards Issue 1.
6. **`OtlpExporterOptions_Endpoint_FromAppsettings_MalformedUri_IsRejected`**
   (new; `OtlpExporterOptionsTests.cs`). Tier 1. Mechanism:
   DirectProperty + EventSource (optional). Pins that
   `TryGetUriValue` behaves identically for env var vs `IConfiguration`
   sources. Guards Issues 1, 6.

All invalid-input recommendations carry the code comment: "Expected to
change under Issue 1 (`IValidateOptions<T>` for reload protection; deferred; no `ValidateOnStart`)."

### 3.4 Reload no-op baseline

Shared pathway spec applies; see
[`../pathways/reload-no-op-baseline.md`](../pathways/reload-no-op-baseline.md).

1. **`OtlpExporterOptions_ReloadOfConfiguration_DoesNotChangeBuiltExporterEndpoint`**
   (new; `UseOtlpExporterExtensionTests.cs`). Tier 2. Mechanism:
   DI + Mock (mock transport client to capture the endpoint the
   exporter actually used). Uses `IConfigurationRoot.Reload()` against
   an `Memory`/`InMemoryCollection` `IConfiguration`. Asserts the
   captured endpoint matches the pre-reload value.
   - Guards Issues 17, 23.
   - Code-comment hint: "BASELINE: pins no-op reload. Expected to flip
     under Issue 23 (`ReloadableOtlpExportClient`)."
2. **`OtlpExporterOptions_ReloadOfConfiguration_DoesNotChangeBuiltExporterTimeout`**,
   **`_Headers`**, **`_Protocol`** - three companion tests in the same
   file, each pinning one property. All Tier 2. Mechanism: for
   `Timeout` and `Headers`, Mock at `HttpClient` level; for `Protocol`
   the client type (gRPC vs HTTP) is the observable - type-check
   suffices. Guards Issues 17, 23.
3. **`OtlpExporterOptions_OnChangeSubscription_FiresOnReload_ButExporterUnchanged`**
   (new; same file). Tier 2. Mechanism: DI + subscription assertion +
   Mock. Pins that `IOptionsMonitor<OtlpExporterOptions>.OnChange`
   does fire on reload (so subscribers added by a future
   `ReloadableOtlpExportClient` will receive notifications) while the
   current pipeline does not act on them. Guards Issue 17.

Risk vs reward for 3.4: moderate effort; high value - without this
suite, Issue 23 has no visible test delta when it lands.

### 3.5 Consumer-observed effects currently missing

1. **`OtlpExporterOptions_TimeoutMilliseconds_AppliedToDefaultHttpClient`**
   (new; `OtlpExporterOptionsTests.cs` or `OtlpExporterOptionsExtensionsTests.cs`).
   Tier 1. Mechanism: DirectProperty - invokes `DefaultHttpClientFactory`,
   asserts `HttpClient.Timeout`. Closes the gap where only the
   `TransmissionHandler` copy is currently tested. Guards Issue 1.
2. **`OtlpExporterOptions_Endpoint_AppendsSignalPath_ObservedAtExportClient`**
   (new; `OtlpExporterOptionsExtensionsTests.cs` or a new helper test
   in the export-client tests). Tier 2. Mechanism: InternalAccessor
   (`OtlpExportClient.Endpoint` is readable via the existing internal
   field in Session 0a Sec.4.G) - avoids wire-level setup. Pins that
   `AppendSignalPathToEndpoint = true` + `Protocol = HttpProtobuf`
   leads to `/v1/traces` (etc.) suffixing. Guards Issues 1, 14.
3. **`OtlpExporterOptions_ExportProcessorType_Simple_WiresSimpleProcessor`**
   and **`_Batch_WiresBatchProcessor`** (new; `OtlpTraceExporterTests.cs`).
   Tier 2. Mechanism: InternalAccessor on `TracerProviderSdk`
   processors list (already used by `_DisposalTest` in Session 0a Sec.3.C).
   Guards Issues 1, 14.
4. **`OtlpExporterOptions_BatchExportProcessorOptions_FlowsToBuiltProcessor`**
   (new; `OtlpTraceExporterTests.cs`). Tier 2. Mechanism:
   InternalAccessor + Reflection fallback for
   `BatchExportProcessor<T>.scheduledDelayMilliseconds`. Pins the flow.
   Guards Issues 1, 21.

### 3.6 Default-state snapshot (pilot-dependent)

1. **`OtlpExporterOptions_Default_Snapshot`** (new;
   `OtlpExporterOptionsTests.cs` or a dedicated `Snapshots/` subfolder
   per the snapshot-library choice in entry-doc Appendix A).
   - Tier 1. Mechanism: Snapshot (library TBD by maintainers; entry
     doc recommends piloting on `ExperimentalOptions` first -
     `OtlpExporterOptions` is a reasonable second pilot because its
     surface is the largest).
   - Guards Issues 1, 4, 14.
   - Code-comment hint: "BASELINE: pins whole-options shape.
     Snapshot update expected on any additive change; reviewer confirms
     intent."
   - Risk vs reward: low per-test cost once the library is chosen;
     high value for catching silent default drift.

### Prerequisites and dependencies

- 3.1 and 3.2 depend on the env-var isolation pattern decision
  (entry-doc Section 5) - new tests that set env vars for the duration
  of a DI test need a fixture or `[Collection]` grouping; the existing
  `IDisposable`-based pattern in `OtlpExporterOptionsTests` can be
  reused.
- 3.4 depends on the reload pathway file
  ([`../pathways/reload-no-op-baseline.md`](../pathways/reload-no-op-baseline.md))
  landing first so the four tests can follow a shared template.
- 3.6 depends on the snapshot-library selection
  ([entry doc Appendix A](../../configuration-test-coverage.md#appendix-a---snapshot-library-comparison)).

---

## Guards issues

This file specifies baseline tests that guard the following entries in
[`../../configuration-proposed-issues.md`](../../configuration-proposed-issues.md):

- **Issue 1** - Add `IValidateOptions<T>` for reload protection (no `ValidateOnStart`; deferred) for all
  options classes. Guarded by: Sections 3.1, 3.2, 3.3, 3.5.
- **Issue 4** - Fix AOT bug: reflection-based binding in
  `OtlpExporterBuilder.cs`. Guarded by: Sections 3.2, 3.6 (defaults
  snapshot guards the binding output shape).
- **Issue 6** - Add diagnostic logging for `RegisterOptionsFactory`
  silent skip. Guarded by: Section 3.3 (`_MalformedUri_IsRejected` row
  touches `OpenTelemetryProtocolExporterEventSource.Log`).
- **Issue 14** - Register OTLP exporter component factories. Guarded by:
  Sections 3.2, 3.5 (consumer-observed effects + `UseOtlpExporter`
  cascade).
- **Issue 17** - Design and implement standard `OnChange` subscriber
  pattern. Guarded by: Section 3.4.
- **Issue 21** - Wire `OnChange` for batch and metric export intervals.
  Guarded by: Section 3.5.4 (`BatchExportProcessorOptions` flow).
- **Issue 23** - OTLP exporter reload: `ReloadableOtlpExportClient` and
  `IOptionsMonitor`-aware constructor. Guarded by: Section 3.4.

Reciprocal "Baseline tests required" lines should be added to each of
the issues above, citing this file. Those edits happen in the final
cross-reference sweep, not here.
