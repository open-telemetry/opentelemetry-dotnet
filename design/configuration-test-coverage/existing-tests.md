# Existing Configuration-Adjacent Test Inventory (Session 0a)

**Scope:** Facts-only survey of config-adjacent tests in the three in-scope
test projects. No gap analysis, no recommendations - those live in Session 0b
and downstream files.

**Projects surveyed:**

- `test/OpenTelemetry.Tests/`
- `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/`
- `test/OpenTelemetry.Extensions.Hosting.Tests/`

**In-scope definition applied:** tests that exercise any in-scope options
class (`OtlpExporterOptions`, `OtlpMtlsOptions`, `OtlpTlsOptions`,
`BatchExportActivityProcessorOptions`, `BatchExportLogRecordProcessorOptions`,
`PeriodicExportingMetricReaderOptions`, `OpenTelemetryLoggerOptions`,
`ExperimentalOptions`, `SdkLimitOptions`), read/set `OTEL_*` env vars, use
`IConfiguration` / `IOptions<T>` / `IOptionsMonitor<T>` / `Configure<T>` /
named options, exercise host-builder DI composition that touches config,
touch vendored env-var provider, resource env-var detectors, self-diagnostics
config file parsing, or exercise provider-global switches
(`OTEL_SDK_DISABLED`, `OTEL_METRICS_EXEMPLAR_FILTER`).

Out of scope: pure algorithm tests, wire-encoding correctness tests unless
env-var-driven, pure data-structure tests.

**Survey caveats:** tables were produced by three parallel Explore agents -
one per project. Line numbers are captured where the agent extracted them;
some audit rows cite file + method only. Theory methods are listed once. A
small number of per-signal OTLP exporter tests (in `OtlpTraceExporterTests`,
`OtlpLogExporterTests`, `OtlpMetricsExporterTests`) were not in the agent's
main table but are included in Sec.1.B below from a spot-check of
`[Fact]`/`[Theory]` enumeration.

---

## 1. Per-test-project tables

### 1.A  `test/OpenTelemetry.Tests/`

#### Trace/BatchExportActivityProcessorOptionsTests.cs

| TestMethod | Scenario summary | Options class | Pathway(s) |
| --- | --- | --- | --- |
| BatchExportProcessorOptions_Defaults | Default values assignment | BatchExportActivityProcessorOptions | - |
| BatchExportProcessorOptions_EnvironmentVariableOverride | `OTEL_BSP_*` env vars override defaults | BatchExportActivityProcessorOptions | env var |
| BatchExportProcessorOptions_UsingIConfiguration | IConfiguration binding (`AddInMemoryCollection`) | BatchExportActivityProcessorOptions | IConfiguration, appsettings |
| BatchExportProcessorOptions_InvalidEnvironmentVariableOverride | Invalid env var falls back to defaults | BatchExportActivityProcessorOptions | env var |
| BatchExportProcessorOptions_SetterOverridesEnvironmentVariable | Programmatic setter takes precedence | BatchExportActivityProcessorOptions | env var |
| BatchExportProcessorOptions_EnvironmentVariableNames | Verify `OTEL_BSP_*` constant names | BatchExportActivityProcessorOptions | - |

#### Logs/BatchExportLogRecordProcessorOptionsTests.cs

| TestMethod | Scenario summary | Options class | Pathway(s) |
| --- | --- | --- | --- |
| BatchExportLogRecordProcessorOptions_Defaults | Default values assignment | BatchExportLogRecordProcessorOptions | - |
| BatchExportLogRecordProcessorOptions_EnvironmentVariableOverride | `OTEL_BLRP_*` env vars override defaults | BatchExportLogRecordProcessorOptions | env var |
| ExportLogRecordProcessorOptions_UsingIConfiguration | IConfiguration binding | BatchExportLogRecordProcessorOptions | IConfiguration, appsettings |
| BatchExportLogRecordProcessorOptions_SetterOverridesEnvironmentVariable | Programmatic setter precedence | BatchExportLogRecordProcessorOptions | env var |
| BatchExportLogRecordProcessorOptions_EnvironmentVariableNames | Verify `OTEL_BLRP_*` constant names | BatchExportLogRecordProcessorOptions | - |

#### Internal/PeriodicExportingMetricReaderHelperTests.cs

| TestMethod | Scenario summary | Options class | Pathway(s) |
| --- | --- | --- | --- |
| CreatePeriodicExportingMetricReader_Defaults | Default reader config | PeriodicExportingMetricReaderOptions | - |
| CreatePeriodicExportingMetricReader_Defaults_WithTask | Defaults with threading disabled | PeriodicExportingMetricReaderOptions | - |
| CreatePeriodicExportingMetricReader_TemporalityPreference_FromOptions | TemporalityPreference option applied | PeriodicExportingMetricReaderOptions | - |
| CreatePeriodicExportingMetricReader_ExportIntervalMilliseconds_FromOptions | Programmatic interval precedence | PeriodicExportingMetricReaderOptions | - |
| CreatePeriodicExportingMetricReader_ExportTimeoutMilliseconds_FromOptions | Programmatic timeout precedence | PeriodicExportingMetricReaderOptions | - |
| CreatePeriodicExportingMetricReader_ExportIntervalMilliseconds_FromEnvVar | `OTEL_METRIC_EXPORT_INTERVAL` env var | PeriodicExportingMetricReaderOptions | env var |
| CreatePeriodicExportingMetricReader_ExportTimeoutMilliseconds_FromEnvVar | `OTEL_METRIC_EXPORT_TIMEOUT` env var | PeriodicExportingMetricReaderOptions | env var |
| CreatePeriodicExportingMetricReader_FromIConfiguration | IConfiguration binding | PeriodicExportingMetricReaderOptions | IConfiguration, appsettings |
| EnvironmentVariableNames | Verify `OTEL_METRIC_*` constant names | PeriodicExportingMetricReaderOptions | - |

#### Internal/SelfDiagnosticsConfigParserTests.cs

| TestMethod | Scenario summary | Options class | Pathway(s) |
| --- | --- | --- | --- |
| SelfDiagnosticsConfigParser_TryParseFilePath_Success | Parse LogDirectory from JSON | - | self-diagnostics file |
| SelfDiagnosticsConfigParser_TryParseFilePath_MissingField | Missing LogDirectory handling | - | self-diagnostics file |
| SelfDiagnosticsConfigParser_TryParseFileSize | Parse FileSize from JSON | - | self-diagnostics file |
| SelfDiagnosticsConfigParser_TryParseFileSize_CaseInsensitive | Case-insensitive parsing | - | self-diagnostics file |
| SelfDiagnosticsConfigParser_TryParseFileSize_MissingField | Missing FileSize handling | - | self-diagnostics file |
| SelfDiagnosticsConfigParser_TryParseLogLevel | Parse LogLevel from JSON | - | self-diagnostics file |
| SelfDiagnosticsConfigParser_TryParseFormatMessage_Success | Parse FormatMessage boolean | - | self-diagnostics file |
| SelfDiagnosticsConfigParser_TryParseFormatMessage_CaseInsensitive | Case-insensitive boolean parsing | - | self-diagnostics file |
| SelfDiagnosticsConfigParser_TryParseFormatMessage_MissingField | Missing FormatMessage handling | - | self-diagnostics file |
| SelfDiagnosticsConfigParser_TryParseFormatMessage_InvalidValue | Invalid boolean value handling | - | self-diagnostics file |
| SelfDiagnosticsConfigParser_TryParseFormatMessage_UnquotedBoolean | Unquoted boolean parsing | - | self-diagnostics file |

#### Internal/SelfDiagnosticsConfigRefresherTests.cs

| TestMethod | Scenario summary | Options class | Pathway(s) |
| --- | --- | --- | --- |
| SelfDiagnosticsConfigRefresher_OmitAsConfigured | Config file filters omit non-error events | - | self-diagnostics file |
| SelfDiagnosticsConfigRefresher_CaptureAsConfigured | Config file filters capture error events | - | self-diagnostics file |

#### Resources/OtelEnvResourceDetectorTests.cs

| TestMethod | Scenario summary | Options class | Pathway(s) |
| --- | --- | --- | --- |
| OtelEnvResource_EnvVarKey | `OTEL_RESOURCE_ATTRIBUTES` key constant | - | resource env var |
| OtelEnvResource_NullEnvVar | Empty resource when env var unset | - | resource env var |
| OtelEnvResource_WithEnvVar_1 | Parse Key=Value pairs from env var | - | resource env var |
| OtelEnvResource_WithEnvVar_2 | Parse malformed pairs (skip invalid) | - | resource env var |
| OtelEnvResource_WithEnvVar_Decoding | URL decoding of values (Theory: 6 cases) | - | resource env var |
| OtelEnvResource_UsingIConfiguration | IConfiguration binding | - | resource env var, IConfiguration |

#### Resources/OtelServiceNameEnvVarDetectorTests.cs

| TestMethod | Scenario summary | Options class | Pathway(s) |
| --- | --- | --- | --- |
| OtelServiceNameEnvVar_EnvVarKey | `OTEL_SERVICE_NAME` key constant | - | resource env var |
| OtelServiceNameEnvVar_Null | Empty resource when env var unset | - | resource env var |
| OtelServiceNameEnvVar_WithValue | Parse service.name from env var | - | resource env var |
| OtelServiceNameEnvVar_UsingIConfiguration | IConfiguration binding | - | resource env var, IConfiguration |

#### Trace/TracerProviderBuilderBaseTests.cs

| TestMethod | Scenario summary | Options class | Pathway(s) |
| --- | --- | --- | --- |
| TracerProviderIsExpectedType | `OTEL_SDK_DISABLED=true` -> `NoopTracerProvider` (Theory: 3) | - | global switch |
| AddInstrumentationInvokesFactoryTest | Instrumentation factory invoked during build | - | DI composition |
| AddInstrumentationValidatesInputTest | Validates instrumentation parameters | - | - |

#### Logs/LoggerProviderBuilderBaseTests.cs

| TestMethod | Scenario summary | Options class | Pathway(s) |
| --- | --- | --- | --- |
| LoggerProviderIsExpectedType | `OTEL_SDK_DISABLED=true` -> `NoopLoggerProvider` (Theory: 3) | - | global switch |

#### Metrics/MeterProviderBuilderBaseTests.cs

| TestMethod | Scenario summary | Options class | Pathway(s) |
| --- | --- | --- | --- |
| LoggerProviderIsExpectedType (sic) | `OTEL_SDK_DISABLED=true` -> `NoopMeterProvider` (Theory: 3) | - | global switch |

#### Trace/TracerProviderBuilderExtensionsTests.cs (config-relevant subset)

| TestMethod | Scenario summary | Options class | Pathway(s) |
| --- | --- | --- | --- |
| ConfigureBuilderIConfigurationAvailableTest | IConfiguration auto-available in `ConfigureBuilder` | - | IConfiguration |
| ConfigureBuilderIConfigurationModifiableTest | Custom IConfiguration via `ConfigureServices` | - | DI composition, IConfiguration |
| TracerProviderNestedResolutionUsingBuilderTest | Nested `Configure*` calls and DI scope (Theory: 2) | - | DI composition, `Configure<T>` |

#### Logs/LoggerProviderBuilderExtensionsTests.cs (config-relevant subset)

| TestMethod | Scenario summary | Options class | Pathway(s) |
| --- | --- | --- | --- |
| LoggerProviderBuilderNestedResolutionUsingBuilderTest | Nested `Configure*` calls and DI scope (Theory: 2) | - | DI composition, `Configure<T>` |

#### Metrics/MeterProviderBuilderExtensionsTests.cs (config-relevant subset)

| TestMethod | Scenario summary | Options class | Pathway(s) |
| --- | --- | --- | --- |
| ConfigureBuilderIConfigurationAvailableTest | IConfiguration auto-available in `ConfigureBuilder` | - | IConfiguration |
| ConfigureBuilderIConfigurationModifiableTest | Custom IConfiguration via `ConfigureServices` | - | DI composition, IConfiguration |

#### Logs/OpenTelemetryLoggingExtensionsTests.cs

| TestMethod | Scenario summary | Options class | Pathway(s) |
| --- | --- | --- | --- |
| ServiceCollectionAddOpenTelemetryNoParametersTest | `AddOpenTelemetry`/`UseOpenTelemetry` invoke options callback (Theory: 2) | OpenTelemetryLoggerOptions | `Configure<T>` |
| ServiceCollectionAddOpenTelemetryConfigureActionTests | Multiple `Configure`/`ConfigureAll` calls (Theory: 6) | OpenTelemetryLoggerOptions | `Configure<T>` |
| UseOpenTelemetryDependencyInjectionTest | `ConfigureServices` + `ConfigureBuilder` DI composition | OpenTelemetryLoggerOptions | DI composition |
| UseOpenTelemetryOptionsOrderingTest | `Configure<T>` ordering: before-bind / extension / after | OpenTelemetryLoggerOptions | IConfiguration, `Configure<T>` |
| TestTrimmingCorrectnessOfOpenTelemetryLoggerOptions | Verify all properties primitive (AOT safety) | OpenTelemetryLoggerOptions | AOT |
| VerifyAddProcessorOverloadWithImplementationFactory | `AddProcessor` with `IServiceProvider` factory | - | DI composition |
| VerifyExceptionIsThrownWhenImplementationFactoryIsNull | Null factory validation | - | DI composition |
| CircularReferenceTest | `ILoggerFactory` + `LoggerProvider` circular dep (Theory: 2) | - | DI composition |
| OptionReloadingTest | `IOptionsMonitor`/`IOptionsSnapshot` usage (Theory: 3) | OpenTelemetryLoggerOptions | reload, IConfiguration |
| MixedOptionsUsageTest | `IOptions`/`IOptionsMonitor`/`IOptionsSnapshot` same instance | OpenTelemetryLoggerOptions | IConfiguration, named options |

#### Logs/LoggerProviderSdkTests.cs (config-relevant subset)

| TestMethod | Scenario summary | Options class | Pathway(s) |
| --- | --- | --- | --- |
| ResourceDetectionUsingIConfigurationTest | `OTEL_SERVICE_NAME` detection via IConfiguration | - | resource env var, IConfiguration |

#### Logs/LoggerFactoryAndResourceBuilderTests.cs (config-relevant subset)

| TestMethod | Scenario summary | Options class | Pathway(s) |
| --- | --- | --- | --- |
| VerifyResourceBuilder_WithServiceNameEnVar | `OTEL_SERVICE_NAME` env var detection | - | resource env var |

#### Metrics/MetricExemplarTests.cs (config-relevant subset)

| TestMethod | Scenario summary | Options class | Pathway(s) |
| --- | --- | --- | --- |
| TestExemplarFilterSetFromConfiguration | Exemplar filter from IConfiguration; programmatic precedence (Theory: 6) | - | IConfiguration, global switch |

#### Trace/TracerProviderSdkTests.cs (config-relevant subset)

| TestMethod | Scenario summary | Options class | Pathway(s) |
| --- | --- | --- | --- |
| TestSamplerSetFromConfiguration | Sampler type and args from IConfiguration (Theory: 11) | - | IConfiguration, global switch |
| TestSamplerConfigurationIgnoredWhenSetProgrammatically | `SetSampler` programmatic call overrides config | - | IConfiguration, global switch |

---

### 1.B  `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/`

#### OtlpExporterOptionsTests.cs

| TestMethod | Scenario summary | Options class(es) | Pathway(s) |
| --- | --- | --- | --- |
| OtlpExporterOptions_Defaults | Default endpoint, protocol, timeout, headers | OtlpExporterOptions | - |
| OtlpExporterOptions_DefaultsForHttpProtobuf | Defaults when protocol set to HttpProtobuf | OtlpExporterOptions | - |
| OtlpExporterOptions_EnvironmentVariableOverride | Env var overrides for all signal types (Theory) | OtlpExporterOptions | env var, spec config definition, signal-specific env var, env-var fallback chain |
| OtlpExporterOptions_UsingIConfiguration | IConfiguration init for all signal types (Theory) | OtlpExporterOptions | IConfiguration, appsettings, spec config definition |
| OtlpExporterOptions_InvalidEnvironmentVariableOverride | Invalid env var values rejected (invalid endpoint, timeout, protocol) | OtlpExporterOptions | env var, IConfiguration |
| OtlpExporterOptions_SetterOverridesEnvironmentVariable | Programmatic setters override env/config | OtlpExporterOptions | env var, IConfiguration |
| OtlpExporterOptions_EndpointGetterUsesProtocolWhenNull | Endpoint fallback by protocol | OtlpExporterOptions | - |
| OtlpExporterOptions_EndpointThrowsWhenSetToNull | Null endpoint validation | OtlpExporterOptions | - |
| OtlpExporterOptions_SettingEndpointToNullResetsAppendSignalPathToEndpoint | Endpoint null assignment behaviour | OtlpExporterOptions | - |
| OtlpExporterOptions_HttpClientFactoryThrowsWhenSetToNull | HttpClientFactory null validation | OtlpExporterOptions | - |
| OtlpExporterOptions_ApplyDefaultsTest | Cascading defaults between options instances | OtlpExporterOptions | - |
| OtlpExporterOptions_MtlsEnvironmentVariables | `OTEL_EXPORTER_OTLP_CERTIFICATE` env var (CA) | OtlpExporterOptions, OtlpMtlsOptions | env var, TLS/mTLS |
| OtlpExporterOptions_MtlsEnvironmentVariables_ClientCertificate | `OTEL_EXPORTER_OTLP_CLIENT_CERTIFICATE` + `CLIENT_KEY` | OtlpExporterOptions, OtlpMtlsOptions | env var, TLS/mTLS |
| OtlpExporterOptions_MtlsEnvironmentVariables_AllCertificates | All three mTLS env vars | OtlpExporterOptions, OtlpMtlsOptions | env var, TLS/mTLS |
| OtlpExporterOptions_MtlsEnvironmentVariables_NoEnvironmentVariables | No mTLS when no env vars set | OtlpExporterOptions, OtlpMtlsOptions | - |
| OtlpExporterOptions_MtlsEnvironmentVariables_UsingIConfiguration | mTLS options via IConfiguration | OtlpExporterOptions, OtlpMtlsOptions | IConfiguration, TLS/mTLS |
| UserAgentProductIdentifier_Default_IsEmpty | Default UserAgentProductIdentifier empty | OtlpExporterOptions | - |
| UserAgentProductIdentifier_DefaultUserAgent_ContainsExporterInfo | Default User-Agent contains OTel-OTLP-Exporter-Dotnet | OtlpExporterOptions | - |
| UserAgentProductIdentifier_WithProductIdentifier_IsPrepended | Custom identifier prepended | OtlpExporterOptions | - |
| UserAgentProductIdentifier_UpdatesStandardHeaders | Updates StandardHeaders | OtlpExporterOptions | - |
| UserAgentProductIdentifier_Rfc7231Compliance_SpaceSeparatedTokens | User-Agent format compliance | OtlpExporterOptions | - |
| UserAgentProductIdentifier_EmptyOrWhitespace_UsesDefaultUserAgent | Empty/whitespace falls back (Theory) | OtlpExporterOptions | - |
| UserAgentProductIdentifier_MultipleProducts_CorrectFormat | Multiple product tokens | OtlpExporterOptions | - |

#### OtlpExporterOptionsExtensionsTests.cs

| TestMethod | Scenario summary | Options class(es) | Pathway(s) |
| --- | --- | --- | --- |
| GetHeaders_NoOptionHeaders_ReturnsStandardHeaders | Empty/null headers (Theory) | OtlpExporterOptions | - |
| GetHeaders_InvalidOptionHeaders_ThrowsArgumentException | Malformed headers (Theory) | OtlpExporterOptions | - |
| GetHeaders_ValidAndUrlEncodedHeaders_ReturnsCorrectHeaders | URL-encoded parsing (Theory) | OtlpExporterOptions | - |
| GetTraceExportClient_SupportedProtocol_ReturnsCorrectExportClient | Protocol -> export client mapping (Theory) | OtlpExporterOptions | - |
| GetTraceExportClient_UnsupportedProtocol_Throws | Invalid protocol | OtlpExporterOptions | - |
| AppendPathIfNotPresent_TracesPath_AppendsCorrectly | Signal-path appending (Theory) | OtlpExporterOptions | - |
| GetTransmissionHandler_InitializesCorrectHandlerExportClientAndTimeoutValue | Transmission handler init (Theory) | ExperimentalOptions | IConfiguration, DI composition |

#### OtlpMtlsOptionsTests.cs

| TestMethod | Scenario summary | Options class(es) | Pathway(s) |
| --- | --- | --- | --- |
| DefaultValues_AreValid | Default OtlpMtlsOptions state (all null/false) | OtlpMtlsOptions | - |
| Properties_CanBeSet | All properties settable | OtlpMtlsOptions | - |
| IsEnabled_ReturnsFalse_WhenNoClientCertificateProvided | IsEnabled false | OtlpMtlsOptions | - |
| IsEnabled_ReturnsTrue_WhenCaCertificateFilePathProvided | IsEnabled true with CA cert | OtlpMtlsOptions | TLS/mTLS |
| IsEnabled_ReturnsFalse_WhenCaCertificateFilePathIsEmpty | Empty string (Theory) | OtlpMtlsOptions | TLS/mTLS |
| IsEnabled_ReturnsTrue_WhenClientCertificateFilePathProvided | IsEnabled true with client cert | OtlpMtlsOptions | TLS/mTLS |
| IsEnabled_ReturnsFalse_WhenClientCertificateFilePathIsEmpty | Empty client cert path (Theory) | OtlpMtlsOptions | TLS/mTLS |

#### OtlpTlsOptionsTests.cs

| TestMethod | Scenario summary | Options class(es) | Pathway(s) |
| --- | --- | --- | --- |
| OtlpTlsOptions_IsTlsEnabled_ReturnsFalse_WhenNoCaCertificatePath | TLS disabled without CA cert | OtlpTlsOptions | TLS/mTLS |
| OtlpTlsOptions_IsTlsEnabled_ReturnsTrue_WhenCaCertificatePathProvided | TLS enabled with CA cert | OtlpTlsOptions | TLS/mTLS |
| OtlpTlsOptions_IsMtlsEnabled_ReturnsFalse_ByDefault | mTLS disabled default | OtlpTlsOptions | TLS/mTLS |
| OtlpMtlsOptions_IsMtlsEnabled_ReturnsTrue_WhenClientCertificateProvided | mTLS enabled with client cert | OtlpMtlsOptions | TLS/mTLS |
| OtlpMtlsOptions_IsMtlsEnabled_ReturnsFalse_WhenOnlyCaCertificateProvided | mTLS not enabled by CA alone | OtlpMtlsOptions | TLS/mTLS |
| OtlpSecureHttpClientFactory_CreatesClient_WithCaCertificateOnly | Secure client with CA cert | OtlpTlsOptions | TLS/mTLS |
| OtlpSecureHttpClientFactory_CreatesClient_WithMtlsClientCertificate | Secure client with client cert | OtlpMtlsOptions | TLS/mTLS |
| OtlpSecureHttpClientFactory_ThrowsArgumentNullException_WhenOptionsIsNull | Null options validation | OtlpTlsOptions, OtlpMtlsOptions | TLS/mTLS |
| OtlpSecureHttpClientFactory_ThrowsInvalidOperationException_WhenTlsNotEnabled | Exception when TLS not enabled | OtlpTlsOptions | TLS/mTLS |
| OtlpCertificateManager_LoadCaCertificate_ThrowsFileNotFoundException | File not found for CA cert | OtlpTlsOptions | TLS/mTLS |
| OtlpCertificateManager_ValidateServerCertificate_ReturnsTrue_WhenNoSslPolicyErrors | Server cert validation (no errors) | OtlpTlsOptions | TLS/mTLS |
| OtlpCertificateManager_ValidateServerCertificate_ReturnsTrue_WithProvidedTrustedCert | Server cert validation with custom trusted cert | OtlpTlsOptions | TLS/mTLS |

#### OtlpSpecConfigDefinitionTests.cs

| TestMethod | Scenario summary | Options class(es) | Pathway(s) |
| --- | --- | --- | --- |
| VerifyKeyNamesMatchSpec | All `OTEL_EXPORTER_OTLP_*` env var names match spec | - | spec config definition |

#### OtlpSpecConfigDefinitionsTests.cs

| TestMethod | Scenario summary | Options class(es) | Pathway(s) |
| --- | --- | --- | --- |
| AllEnvironmentVariableNames_AreUnique | No duplicate env var names across OTLP config | - | spec config definition |

#### SdkLimitOptionsTests.cs

| TestMethod | Scenario summary | Options class(es) | Pathway(s) |
| --- | --- | --- | --- |
| SdkLimitOptionsDefaults | Default SdkLimitOptions values | SdkLimitOptions | - |
| SdkLimitOptionsIsInitializedFromEnvironment | `OTEL_ATTRIBUTE_*`, `OTEL_SPAN_*`, `OTEL_EVENT_*`, `OTEL_LINK_*`, `OTEL_LOGRECORD_*` | SdkLimitOptions | env var |
| SpanAttributeValueLengthLimitFallback | `AttributeValueLengthLimit` -> `SpanAttributeValueLengthLimit` -> `LogRecordAttributeValueLengthLimit` fallback | SdkLimitOptions | env-var fallback chain |
| SpanAttributeCountLimitFallback | `AttributeCountLimit` -> `SpanAttributeCountLimit` -> `SpanEventAttributeCountLimit` -> `SpanLinkAttributeCountLimit` fallback | SdkLimitOptions | env-var fallback chain |
| SdkLimitOptionsUsingIConfiguration | IConfiguration init (no env vars) | SdkLimitOptions | IConfiguration |

#### UseOtlpExporterExtensionTests.cs

| TestMethod | Scenario summary | Options class(es) | Pathway(s) |
| --- | --- | --- | --- |
| UseOtlpExporterDefaultTest | `UseOtlpExporter` with no config produces defaults | OtlpExporterBuilderOptions | UseOtlpExporter, DI composition |
| UseOtlpExporterSetEndpointAndProtocolTest | `UseOtlpExporter(protocol, endpoint)` overload (Theory) | OtlpExporterBuilderOptions | UseOtlpExporter |
| UseOtlpExporterConfigureTest | `UseOtlpExporter` with `Configure<T>` delegate (named + unnamed) (Theory) | OtlpExporterBuilderOptions, BatchExportActivityProcessorOptions, BatchExportLogRecordProcessorOptions, PeriodicExportingMetricReaderOptions | UseOtlpExporter, `Configure<T>`, named options |
| UseOtlpExporterConfigurationTest | `UseOtlpExporter(IConfiguration)` all signals, named/unnamed (Theory) | OtlpExporterBuilderOptions, BatchExportActivityProcessorOptions, BatchExportLogRecordProcessorOptions, PeriodicExportingMetricReaderOptions | UseOtlpExporter, IConfiguration |
| UseOtlpExporterSingleCallsTest | Builds all three providers | - | UseOtlpExporter, DI composition |
| UseOtlpExporterMultipleCallsTest | Multiple calls throw `NotSupportedException` | - | UseOtlpExporter |
| UseOtlpExporterWithAddOtlpExporterLoggingTest | Conflicts with `AddOtlpExporter` (logging) | - | UseOtlpExporter, AddOtlpExporter |
| UseOtlpExporterWithAddOtlpExporterMetricsTest | Conflicts with `AddOtlpExporter` (metrics) | - | UseOtlpExporter, AddOtlpExporter |
| UseOtlpExporterWithAddOtlpExporterTracingTest | Conflicts with `AddOtlpExporter` (tracing) | - | UseOtlpExporter, AddOtlpExporter |
| UseOtlpExporterAddsTracingProcessorToPipelineEndTest | Exporter processor at end of pipeline | - | UseOtlpExporter, DI composition |
| UseOtlpExporterAddsLoggingProcessorToPipelineEndTest | Logging processor at end of pipeline | - | UseOtlpExporter, DI composition |
| UseOtlpExporterRespectsSpecEnvVarsTest | All `OTEL_EXPORTER_OTLP_*` env vars for all signals | OtlpExporterBuilderOptions | UseOtlpExporter, env var, spec config definition, signal-specific env var |
| UseOtlpExporterRespectsSpecEnvVarsSetUsingIConfigurationTest | Reads IConfiguration instead of env vars | OtlpExporterBuilderOptions | UseOtlpExporter, IConfiguration, spec config definition |

#### OtlpExporterHelperExtensionsTests.cs

| TestMethod | Scenario summary | Options class(es) | Pathway(s) |
| --- | --- | --- | --- |
| OtlpExporter_Throws_OnGrpcWithDefaultFactory_ForTracing | `AddOtlpExporter` Grpc without custom HttpClientFactory throws on NETFRAMEWORK/NETSTANDARD2_0 | OtlpExporterOptions | AddOtlpExporter |
| OtlpExporter_Throws_OnGrpcWithDefaultFactory_ForMetrics | Same for metrics | OtlpExporterOptions | AddOtlpExporter |
| OtlpExporter_Throws_OnGrpcWithDefaultFactory_ForLogging | Same for logging | OtlpExporterOptions | AddOtlpExporter |
| OtlpExporter_DoesNotThrow_WhenCustomHttpClientFactoryIsSet_ForTraces | Succeeds with custom HttpClientFactory (traces) | OtlpExporterOptions | AddOtlpExporter |
| OtlpExporter_DoesNotThrow_WhenCustomHttpClientFactoryIsSet_ForMetrics | Succeeds with custom HttpClientFactory (metrics) | OtlpExporterOptions | AddOtlpExporter |
| OtlpExporter_DoesNotThrow_WhenCustomHttpClientFactoryIsSet_ForLogging | Succeeds with custom HttpClientFactory (logging) | OtlpExporterOptions | AddOtlpExporter |

#### OtlpCertificateManagerTests.cs

| TestMethod | Scenario summary | Options class(es) | Pathway(s) |
| --- | --- | --- | --- |
| LoadClientCertificate_ThrowsFileNotFoundException_WhenCertificateFileDoesNotExist | File not found for client cert | - | TLS/mTLS |
| LoadClientCertificate_ThrowsFileNotFoundException_WhenPrivateKeyFileDoesNotExist | File not found for private key | - | TLS/mTLS |
| LoadCaCertificate_ThrowsFileNotFoundException_WhenTrustStoreFileDoesNotExist | File not found for CA cert | - | TLS/mTLS |
| LoadClientCertificate_ThrowsInvalidOperationException_WhenCertificateFileIsEmpty | Invalid cert format | - | TLS/mTLS |
| LoadCaCertificate_ThrowsInvalidOperationException_WhenTrustStoreFileIsEmpty | Invalid CA cert format | - | TLS/mTLS |
| ValidateCertificateChain_DoesNotThrow_WithValidCertificate | Valid cert chain | - | TLS/mTLS |
| ValidateCertificateChain_ReturnsResult_WithValidCertificate | Returns boolean result | - | TLS/mTLS |
| LoadClientCertificate_LoadsFromSeparateFiles | Load cert from separate cert + key files | - | TLS/mTLS |

#### OtlpSecureHttpClientFactoryTests.cs

| TestMethod | Scenario summary | Options class(es) | Pathway(s) |
| --- | --- | --- | --- |
| CreateHttpClient_ThrowsInvalidOperationException_WhenMtlsIsDisabled | Exception when mTLS disabled | OtlpMtlsOptions | TLS/mTLS |
| CreateHttpClient_ThrowsFileNotFoundException_WhenCertificateFileDoesNotExist | File not found for nonexistent client cert | OtlpMtlsOptions | TLS/mTLS |
| CreateHttpClient_ConfiguresClientCertificate_WhenValidCertificateProvided | Client cert configured | OtlpMtlsOptions | TLS/mTLS |
| CreateHttpClient_ConfiguresServerCertificateValidation_WhenCaCertificatesProvided | Server cert validation callback | OtlpMtlsOptions | TLS/mTLS |
| CreateSecureHttpClient_WithServerCertificateValidationEnabled | Validation callback when enabled | OtlpMtlsOptions | TLS/mTLS |
| CreateSecureHttpClient_WithServerCertificateValidationDisabled | Bypass when disabled | OtlpMtlsOptions | TLS/mTLS |
| CreateSecureHttpClient_WithMultipleCaCertificates | Multiple CA certs | OtlpMtlsOptions | TLS/mTLS |
| CreateSecureHttpClient_ConfiguresClientCertificateChain | Client cert chain (cert + key) | OtlpMtlsOptions | TLS/mTLS |
| CreateSecureHttpClient_WithMtlsFullChain | Full mTLS chain | OtlpMtlsOptions | TLS/mTLS |
| CreateSecureHttpClient_WithInvalidCertificatePath | Exception for invalid cert path | OtlpMtlsOptions | TLS/mTLS |
| CreateSecureHttpClient_WithInvalidKeyPath | Exception for invalid key path | OtlpMtlsOptions | TLS/mTLS |
| CreateSecureHttpClient_ThrowsArgumentNullException_WhenOptionsNull | Null options validation | OtlpMtlsOptions | TLS/mTLS |
| SkipTestIfCryptoNotSupported_GracefullyHandlesPlatformNotSupported | Platform-specific crypto limitations | - | TLS/mTLS |

#### OtlpTraceExporterTests.cs (config-relevant subset)

Enumerated from `[Fact]`/`[Theory]` spot-check; subset touching options / named
options / DI / HttpClientFactory.

| TestMethod | Scenario summary | Options class(es) | Pathway(s) |
| --- | --- | --- | --- |
| AddOtlpTraceExporterNamedOptionsSupported | Named options support for `AddOtlpExporter` | OtlpExporterOptions | named options, AddOtlpExporter |
| OtlpExporter_BadArgs | Options validation for bad constructor args | OtlpExporterOptions | - |
| UserHttpFactoryCalled | User-supplied HttpClientFactory invoked | OtlpExporterOptions | DI composition |
| ServiceProviderHttpClientFactoryInvoked | IHttpClientFactory from DI invoked | OtlpExporterOptions | DI composition |
| UseOpenTelemetryProtocolActivityExporterWithCustomActivityProcessor | Custom activity processor via DI | - | DI composition |
| Null_BatchExportProcessorOptions_SupportedTest | Null batch options supported | BatchExportActivityProcessorOptions | - |
| NonnamedOptionsMutateSharedInstanceTest | Unnamed options share instance | OtlpExporterOptions | named options |
| NamedOptionsMutateSeparateInstancesTest | Named options yield separate instances | OtlpExporterOptions | named options |

#### OtlpLogExporterTests.cs (config-relevant subset)

| TestMethod | Scenario summary | Options class(es) | Pathway(s) |
| --- | --- | --- | --- |
| AddOtlpExporterWithNamedOptions | Named options for log exporter | OtlpExporterOptions | named options, AddOtlpExporter |
| UserHttpFactoryCalledWhenUsingHttpProtobuf | User HttpClientFactory invoked (HttpProtobuf) | OtlpExporterOptions | DI composition |
| AddOtlpExporterSetsDefaultBatchExportProcessor | Default batch processor wired | BatchExportLogRecordProcessorOptions | AddOtlpExporter, DI composition |

#### OtlpMetricsExporterTests.cs (config-relevant subset)

| TestMethod | Scenario summary | Options class(es) | Pathway(s) |
| --- | --- | --- | --- |
| TestAddOtlpExporter_SetsCorrectMetricReaderDefaults | Default reader config applied | PeriodicExportingMetricReaderOptions | AddOtlpExporter |
| TestAddOtlpExporter_NamedOptions | Named options for metric exporter | OtlpExporterOptions | named options, AddOtlpExporter |
| UserHttpFactoryCalled | User HttpClientFactory invoked | OtlpExporterOptions | DI composition |
| ServiceProviderHttpClientFactoryInvoked | IHttpClientFactory from DI invoked | OtlpExporterOptions | DI composition |
| TemporalityPreferenceFromEnvVar (Theory at ~line 734) | `OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE` env var | PeriodicExportingMetricReaderOptions | env var, signal-specific env var |
| TemporalityPreferenceFromIConfiguration (Theory at ~line 765) | Same via IConfiguration | PeriodicExportingMetricReaderOptions | IConfiguration |

> Method names near lines 734/765 are inferred from the Theory
> `InlineData("cuMulative", ...)` values; actual method identifiers were not
> captured in the spot-check.

---

### 1.C  `test/OpenTelemetry.Extensions.Hosting.Tests/`

| TestMethod | Scenario summary | Options class(es) | Pathway(s) |
| --- | --- | --- | --- |
| **EventSourceTests.cs** | | | |
| EventSourceTests_HostingExtensionsEventSource | Validates EventSource IDs for `HostingExtensionsEventSource` | - | - |
| **InMemoryExporterMetricsExtensionsTests.cs** | | | |
| DeferredMeterProviderBuilder_WithMetric | Deferred builder + in-memory exporter (`List<Metric>`) | - | DI composition, WithMetrics, host builder |
| DeferredMeterProviderBuilder_WithMetricSnapshot | Deferred builder + in-memory exporter (`List<MetricSnapshot>`) | - | DI composition, WithMetrics, host builder |
| **OpenTelemetryBuilderTests.cs** | | | |
| ConfigureResourceTest | `ConfigureResource` applies across tracing/metrics/logging | - | ConfigureResource, DI composition, AddOpenTelemetry |
| ConfigureResourceServiceProviderTest | `ConfigureResource` with `IResourceDetector` via DI | - | ConfigureResource, DI composition |
| **OpenTelemetryMetricsBuilderExtensionsTests.cs** | | | |
| EnableMetricsTest{Byte,Short,Int,Long,Float,Double} | `EnableMetrics` per numeric type | - | DI composition, WithMetrics, host builder |
| EnableMetricsWithAddMeterTest | `EnableMetrics` + `AddSdkMeter` combination | - | DI composition, WithMetrics, host builder |
| ReloadOfMetricsViaIConfigurationWithExportCleanupTest | IConfiguration enable/disable with reload + export cleanup (Delta/Cumulative) | `MetricsOptions` (via `IOptionsMonitor`) | IConfiguration, reload, host builder, WithMetrics |
| ReloadOfMetricsViaIConfigurationWithoutExportCleanupTest | IConfiguration enable/disable with reload (no cleanup) | `MetricsOptions` | IConfiguration, reload, host builder, WithMetrics |
| WhenOpenTelemetrySdkIsDisabledExceptionNotThrown | `OTEL_SDK_DISABLED=true` -> noop meter provider | - | env var, global switch, DI composition, WithMetrics |
| **OpenTelemetryServicesExtensionsTests.cs** | | | |
| AddOpenTelemetry_StartWithoutProvidersDoesNotThrow | Host start without providers | - | DI composition, host builder |
| AddOpenTelemetry_StartWithExceptionsThrows | Exceptions in deferred Configure callback propagate | - | DI composition, host builder |
| AddOpenTelemetry_WithTracing_SingleProviderForServiceCollectionTest | Multiple `WithTracing` -> single provider | - | DI composition, WithTracing |
| AddOpenTelemetry_WithTracing_DisposalTest | `TracerProviderSdk` disposal mirrors service provider | - | DI composition, WithTracing |
| AddOpenTelemetry_WithTracing_HostConfigurationHonoredTest | Deferred callback receives host IConfiguration | - | IConfiguration, host builder, WithTracing |
| AddOpenTelemetry_WithTracing_NestedResolutionUsingConfigureTest | Nested resolution cannot get `TracerProvider` during configure | - | DI composition, WithTracing |
| AddOpenTelemetry_WithMetrics_SingleProviderForServiceCollectionTest | Same for metrics | - | DI composition, WithMetrics |
| AddOpenTelemetry_WithMetrics_DisposalTest | Same for metrics | - | DI composition, WithMetrics |
| AddOpenTelemetry_WithMetrics_HostConfigurationHonoredTest | Same for metrics | - | IConfiguration, host builder, WithMetrics |
| AddOpenTelemetry_WithMetrics_NestedResolutionUsingConfigureTest | Same for metrics | - | DI composition, WithMetrics |
| AddOpenTelemetry_WithLogging_SingleProviderForServiceCollectionTest | Same for logging | - | DI composition, WithLogging |
| AddOpenTelemetry_WithLogging_DisposalTest | Same for logging | - | DI composition, WithLogging |
| AddOpenTelemetry_WithLogging_HostConfigurationHonoredTest | Same for logging | - | IConfiguration, host builder, WithLogging |
| AddOpenTelemetry_WithLogging_NestedResolutionUsingConfigureTest | Same for logging | - | DI composition, WithLogging |
| AddOpenTelemetry_HostedServiceOrder_DoesNotMatter | Hosted-service order vs OTel init | - | IHostedService, host builder, WithTracing, DI composition |

---

## 2. Env-var isolation audit

Tests are classified by how they isolate `OTEL_*` env var mutation from the
surrounding process/test-run.

### 2.A  `snapshot/restore` via IDisposable class-level pattern

Test class implements `IDisposable`; constructor clears/snapshots env vars,
`Dispose` restores.

- `test/OpenTelemetry.Tests/Trace/BatchExportActivityProcessorOptionsTests.cs`:
  `BatchExportActivityProcessorOptionsTests` (ClearEnvVars in ctor, Dispose)
- `test/OpenTelemetry.Tests/Logs/BatchExportLogRecordProcessorOptionsTests.cs`:
  `BatchExportLogRecordProcessorOptionsTests` (ClearEnvVars in ctor, Dispose)
- `test/OpenTelemetry.Tests/Internal/PeriodicExportingMetricReaderHelperTests.cs`:
  `PeriodicExportingMetricReaderHelperTests` (ClearEnvVars in ctor, Dispose)
- `test/OpenTelemetry.Tests/Resources/OtelEnvResourceDetectorTests.cs`:
  `OtelEnvResourceDetectorTests` (SetEnvironmentVariable in ctor, Dispose
  restores)
- `test/OpenTelemetry.Tests/Resources/OtelServiceNameEnvVarDetectorTests.cs`:
  `OtelServiceNameEnvVarDetectorTests` (SetEnvironmentVariable in ctor, Dispose
  restores)
- `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/OtlpExporterOptionsTests.cs`:
  `OtlpExporterOptionsTests` (IDisposable, constructor clears; paired with
  `[Collection("EnvVars")]`)
- `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/SdkLimitOptionsTests.cs`:
  `SdkLimitOptionsTests` (IDisposable)
- `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/UseOtlpExporterExtensionTests.cs`:
  `UseOtlpExporterExtensionTests` (IDisposable; paired with
  `[Collection("EnvVars")]`)

### 2.B  `EnvironmentVariableScope` helper (using-block snapshot/restore)

Tests use `using (new EnvironmentVariableScope(...))` from
`test/OpenTelemetry.Tests/EnvironmentVariableScope.cs`.

- `test/OpenTelemetry.Tests/Trace/TracerProviderBuilderBaseTests.cs`:
  `TracerProviderIsExpectedType`
- `test/OpenTelemetry.Tests/Logs/LoggerProviderBuilderBaseTests.cs`:
  `LoggerProviderIsExpectedType`
- `test/OpenTelemetry.Tests/Metrics/MeterProviderBuilderBaseTests.cs`:
  `LoggerProviderIsExpectedType` (sic)
- `test/OpenTelemetry.Tests/Trace/TracerProviderBuilderExtensionsTests.cs`:
  `ConfigureBuilderIConfigurationAvailableTest`
- `test/OpenTelemetry.Tests/Metrics/MeterProviderBuilderExtensionsTests.cs`:
  `ConfigureBuilderIConfigurationAvailableTest`
- `test/OpenTelemetry.Tests/Logs/LoggerFactoryAndResourceBuilderTests.cs`:
  `VerifyResourceBuilder_WithServiceNameEnVar`
- `test/OpenTelemetry.Extensions.Hosting.Tests/OpenTelemetryMetricsBuilderExtensionsTests.cs`:
  `WhenOpenTelemetrySdkIsDisabledExceptionNotThrown` (linked via `<Compile
  Link>` in csproj)

### 2.C  `[Collection]` attribute (serializes across tests sharing the collection)

Collections observed:

- `"EnvVars"`: serialises env-var-mutating tests in the OTLP test project:
  - `OtlpExporterOptionsTests` (OtlpExporterOptionsTests.cs:8)
  - `OtlpMetricsExporterTests` (OtlpMetricsExporterTests.cs:20)
  - `UseOtlpExporterExtensionTests` (UseOtlpExporterExtensionTests.cs:15)
- `"xUnitCollectionPreventingTestsThatDependOnSdkConfigurationFromRunningInParallel"`:
  serialises SDK-config-sensitive tests:
  - `OtlpTraceExporterTests` (OtlpTraceExporterTests.cs:20). Class static
    constructor sets
    `Activity.DefaultIdFormat`/`Activity.ForceDefaultIdFormat`.

No `[CollectionDefinition]` classes were found in any of the three projects;
`[Collection("EnvVars")]` and the SDK-config collection are implicit
(attribute-only) collections.

### 2.D  `unsafe` (sets env var with no isolation)

- `test/OpenTelemetry.Tests/Metrics/MetricExemplarTests.cs`:
  `TestExemplarFilterSetFromConfiguration` (uses `AddInMemoryCollection` ->
  IConfiguration-only; does not set env vars. **Note:** the agent flagged this
  as a potential isolation gap because it runs adjacent to env-var-sensitive
  tests in the same class without a collection attribute; strictly it is not an
  env-var mutator. Re-classify in Session 0b if relevant.)

The OTLP agent also flagged the four `OtlpExporterOptions_Mtls*` tests
(OtlpExporterOptionsTests.cs:271,291,314) as "unsafe" - this is a
mis-classification: these tests belong to `OtlpExporterOptionsTests` which
does have the class-level IDisposable snapshot/restore pattern plus the
`[Collection("EnvVars")]` attribute (see Sec.2.A). They are protected; the
"unsafe" line in the raw agent output is superseded by the class-level
isolation facts.

### 2.E  `unknown` (not obviously any of the above)

None surfaced in the three surveys.

### 2.F  Process-isolation (child process / separate AppDomain / AssemblyLoadContext)

None found. Every currently configured env-var test relies on in-process
isolation (per-class IDisposable, `EnvironmentVariableScope`, or
`[Collection]`).

---

## 3. Observation-mechanism audit

For each in-scope test, how does it assert the outcome of the configured
options?

### 3.A  `direct property` - assert on the options instance's public property

- `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/OtlpExporterOptionsTests.cs`:
  ~23 methods; asserts `Endpoint`, `Protocol`, `TimeoutMilliseconds`, `Headers`,
  `MtlsOptions.*`, etc.
- `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/OtlpMtlsOptionsTests.cs`:
  7 methods; `ClientCertificatePath`, `ClientKeyPath`, `CaCertificatePath`,
  `IsEnabled`.
- `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/OtlpTlsOptionsTests.cs`:
  12 methods; `IsTlsEnabled`, `IsMtlsEnabled`.
- `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/OtlpSpecConfigDefinitionTests.cs`:
  string-literal comparison against `OtlpSpecConfigDefinitions` constants.
- Many `OpenTelemetry.Tests` options tests (defaults / env-var override /
  IConfiguration) assert directly on options properties after constructing the
  options from `IConfiguration` / env vars: `BatchExportProcessorOptions_*`,
  `BatchExportLogRecordProcessorOptions_*`,
  `PeriodicExportingMetricReaderHelperTests.*`,
  `SdkLimitOptionsTests.SdkLimitOptionsDefaults`, `OtelEnvResource_*`,
  `OtelServiceNameEnvVar_*`.

### 3.B  `DI IOptions<T>` / `IOptionsMonitor<T>` / `IOptionsSnapshot<T>`

Tests that resolve options from `IServiceProvider`:

- `OpenTelemetryLoggingExtensionsTests.ServiceCollectionAddOpenTelemetryNoParametersTest`:
  resolves `IOptionsMonitor<OpenTelemetryLoggerOptions>`.
- `OpenTelemetryLoggingExtensionsTests.OptionReloadingTest`: Theory over
  `IOptions` / `IOptionsMonitor` / `IOptionsSnapshot`.
- `OpenTelemetryLoggingExtensionsTests.MixedOptionsUsageTest`: resolves all
  three via DI.
- `OpenTelemetryLoggingExtensionsTests.UseOpenTelemetryOptionsOrderingTest`:
  `IConfiguration` + `Configure<T>` via DI.
- `LoggerProviderSdkTests.ResourceDetectionUsingIConfigurationTest`:
  IConfiguration via DI.
- `TracerProviderBuilderExtensionsTests.ConfigureBuilderIConfigurationModifiableTest`:
  custom IConfiguration injected via `ConfigureServices`.
- `MeterProviderBuilderExtensionsTests.ConfigureBuilderIConfigurationModifiableTest`:
  same.
- `OtlpExporterOptionsExtensionsTests.GetTransmissionHandler_InitializesCorrectHandlerExportClientAndTimeoutValue`:
  constructs `ExperimentalOptions(configuration)`.
- `UseOtlpExporterExtensionTests.UseOtlpExporterDefaultTest`:
  `IOptionsMonitor<OtlpExporterBuilderOptions>`.
- `UseOtlpExporterExtensionTests.UseOtlpExporterConfigureTest`:
  `IOptionsMonitor<...>.Get(name)` for named options.
- `UseOtlpExporterExtensionTests.UseOtlpExporterConfigurationTest`:
  `IOptionsMonitor<OtlpExporterBuilderOptions>`,
  `IOptionsMonitor<LogRecordExportProcessorOptions>`,
  `IOptionsMonitor<MetricReaderOptions>`,
  `IOptionsMonitor<ActivityExportProcessorOptions>`.
- `OpenTelemetryMetricsBuilderExtensionsTests.ReloadOfMetricsViaIConfigurationWith*Test`:
  resolves `IOptionsMonitor<MetricsOptions>`.
- `OpenTelemetryServicesExtensionsTests.AddOpenTelemetry_With{Tracing,Metrics,Logging}_HostConfigurationHonoredTest`:
  Configure callbacks receive `IConfiguration` via host DI.

### 3.C  `reflection private field`

- `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/OtlpMetricsExporterTests.cs:55`:
  `BindingFlags.NonPublic` to access `MeterProviderSdk.Reader` (not strictly a
  private options field, but reflection-based observation of internal state).
- `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/OtlpSecureHttpClientFactoryTests.cs:57`:
  `BindingFlags.NonPublic` to access `HttpMessageInvoker._handler`.
- `test/OpenTelemetry.Extensions.Hosting.Tests/OpenTelemetryServicesExtensionsTests.cs`:
  `AddOpenTelemetry_With{Tracing,Metrics,Logging}_DisposalTest` access
  `OwnedServiceProvider` and `Disposed` on
  `TracerProviderSdk`/`MeterProviderSdk`/`LoggerProviderSdk` via casting
  (internal types, enabled by `InternalsVisibleTo`; not strictly private-field
  reflection but uses internal-member access).

### 3.D  `mock exporter / test processor` (behavioural)

- `LoggerProviderSdkTests.{ForceFlushTest, ThreadStaticPoolUsedByProviderTests,
  SharedPoolUsedByProviderTests}`: `InMemoryExporter` and
  `SimpleLogRecordExportProcessor`.
- `OpenTelemetryLoggingExtensionsTests.UseOpenTelemetryDependencyInjectionTest`:
  local `TestLogProcessor`.
- `OpenTelemetryLoggingExtensionsTests.CircularReferenceTest`:
  `TestLogProcessorWithILoggerFactoryDependency`.
- `InMemoryExporterMetricsExtensionsTests.DeferredMeterProviderBuilder_With*`:
  in-memory metrics exporter.
- `OpenTelemetryMetricsBuilderExtensionsTests.EnableMetricsTest*`: in-memory
  exporter captures counts.
- `OpenTelemetryServicesExtensionsTests.AddOpenTelemetry_HostedServiceOrder_DoesNotMatter`:
  in-memory trace exporter.
- `UseOtlpExporterExtensionTests.UseOtlpExporterAdds{Tracing,Logging}ProcessorToPipelineEndTest`:
  local `TestLogRecordProcessor` and similar.

### 3.E  `wire` (in-proc listener / mock collector / HTTP server)

- `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/MockCollectorIntegrationTests.cs`:
  AspNetCore-hosted HTTP/gRPC mock collector; verifies export-request receipt.
- `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/IntegrationTest/IntegrationTests.cs`:
  theory-based end-to-end tests requiring live `OTEL_COLLECTOR_HOSTNAME` env
  var.
- `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/TestHttpMessageHandler.cs`:
  helper used for HTTP request inspection.

### 3.F  `event source`

- `test/OpenTelemetry.Extensions.Hosting.Tests/OpenTelemetryMetricsBuilderExtensionsTests.cs`:
  `ReloadOfMetricsViaIConfiguration*` filter on specific `EventId`s via
  `InMemoryEventListener`.
- `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/EventSourceTests.cs`:
  OTLP EventSource ID validation (not config-adjacent per se but catalogued here
  for completeness).
- `test/OpenTelemetry.Tests/EventSourceTests.cs`: `EventSourceTestHelper` +
  `InMemoryEventListener` usage is common across SDK event-source tests.

### 3.G  `behavioural side effect` (type check, built-pipeline behaviour)

- `TracerProviderBuilderBaseTests.TracerProviderIsExpectedType`:
  `Assert.IsType<NoopTracerProvider>` vs `TracerProviderSdk`.
- `LoggerProviderBuilderBaseTests.LoggerProviderIsExpectedType`: same for logger
  provider.
- `MeterProviderBuilderBaseTests.LoggerProviderIsExpectedType`: same for meter
  provider.
- `OpenTelemetryBuilderTests.ConfigureResourceTest` /
  `ConfigureResourceServiceProviderTest`: asserts on `Resource.Attributes` of
  resolved providers.

### 3.H  `mixed` (combine multiple mechanisms)

- `OtlpTraceExporterTests.*`: direct property + DI + reflection on processor
  placement (3 methods in the file).
- `OtlpMetricsExporterTests.*`: direct property + DI verification (2 methods).
- `OtlpLogExporterTests.*`: direct property + DI `Configure<T>` invocation
  counting (1 method).
- `OpenTelemetryMetricsBuilderExtensionsTests.ReloadOfMetricsViaIConfiguration*`:
  in-memory exporter + `IOptionsMonitor` + IConfiguration reload + event-source
  assertion.

---

## 4. Test-infrastructure audit

### 4.A  Fixtures (`IClassFixture` / `ICollectionFixture` / `[CollectionDefinition]`)

None found. No xUnit class fixtures or collection fixtures exist in any of
the three test projects. Isolation is achieved via IDisposable class-level
patterns and attribute-only `[Collection]` grouping (Sec.2.C).

### 4.B  Collections (attribute-only)

- `[Collection("EnvVars")]`: OTLP project, 3 classes (Sec.2.C).
- `[Collection("xUnitCollectionPreventingTestsThatDependOnSdkConfigurationFromRunningInParallel")]`:
  OTLP project, 1 class.

### 4.C  Base classes

- `test/OpenTelemetry.Tests/Metrics/MetricTestsBase.cs`: public abstract base;
  exposes `BuildMeterProvider` and (via `BuildHost`) host-based metric-provider
  helpers. **Linked into the Hosting test project** via `<Compile Include
  Link>`.
- No other shared base classes found.

### 4.D  Shared helpers in `test/OpenTelemetry.Tests/Shared/` (config-relevant)

- `EnvironmentVariableScope.cs` (project root, not `Shared/`) - internal sealed
  IDisposable. Snapshots current value of a named env var on `new`; restores on
  `Dispose`. **Linked into Hosting test project** via `<Compile Include Link>`;
  not linked into OTLP project.
- `SkipUnlessEnvVarFoundFactAttribute.cs` /
  `SkipUnlessEnvVarFoundTheoryAttribute.cs`: skip `Fact`/`Theory` unless named
  env var is set.
- `DelegatingExporter.cs`, `DelegatingProcessor.cs`: fake components for mock
  observation.
- `TestActivityExportProcessor.cs`, `TestActivityProcessor.cs`,
  `TestSampler.cs`, `RecordOnlySampler.cs`: capture sampling / processing
  parameters.
- `InMemoryEventListener.cs`, `EventSourceTestHelper.cs`,
  `TestEventListener.cs`: EventSource assertion helpers.
- `Utils.cs`: miscellaneous (`GetCurrentMethodName`, etc.).
- `TestHttpServer.cs`: simple HTTP server for wire-level tests.

### 4.E  Helpers in `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/`

- `OtlpTestHelpers.cs`: `AssertOtlpAttributes()` protobuf-attribute-match
  assertion.
- `TestExportClient.cs`: mock `IExportClient` for transmission-handler tests.
- `TestHttpMessageHandler.cs`: mock `HttpMessageHandler` for request/response
  inspection.
- `LoggerExtensions.cs`: logging utilities for test output.
- `GrpcRetryTestCase.cs`, `HttpRetryTestCase.cs`: per-transport retry test
  cases.
- `gen_test_cert.ps1` / `gen_test_cert.sh`: shell scripts referenced from the
  test project; per agent report, **not observed in use** - TLS/mTLS tests
  generate certificates on-the-fly via `System.Security.Cryptography`
  (OtlpTlsOptionsTests.cs, OtlpSecureHttpClientFactoryTests.cs). The scripts
  appear to be artefacts for pre-generated certificate workflows; confirm
  intended use in Session 0b.
- `IntegrationTest/`: end-to-end integration tests against a live collector
  (`OTEL_COLLECTOR_HOSTNAME`).
- `MockCollectorIntegrationTests.cs`: in-process AspNetCore HTTP+gRPC mock
  collector harness.
- `PersistentStorage/`: persistent storage tests (not config-adjacent).

### 4.F  Helpers / linked sources in `test/OpenTelemetry.Extensions.Hosting.Tests/`

No standalone helper classes; the project uses `<Compile Include ...
Link="..."/>`
to embed shared sources from `test/OpenTelemetry.Tests/`:

- `EnvironmentVariableScope.cs` (root)
- `Shared/EventSourceTestHelper.cs`
- `Shared/TestEventListener.cs`
- `Shared/InMemoryEventListener.cs`
- `Shared/Utils.cs`
- `Metrics/MetricTestsBase.cs` (+ `HostingMeterProviderBuilder.cs` and several
  supporting metric-test files)

Test classes contain inline helper types (local `TestResourceDetector`,
`MySampler`, `TestHostedService`, `TestLogProcessor*`) rather than shared
files.

### 4.G  `InternalsVisibleTo` from `src/` to test projects

- `src/OpenTelemetry/` -> `OpenTelemetry.Tests` (PublicKey =
  `$(StrongNamePublicKey)`). Gives the core-SDK tests access to internal types
  (options classes, processors, providers, `DelegatingOptionsFactory`, etc.).
- `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OpenTelemetry.Exporter.OpenTelemetryProtocol.csproj`
  -> `OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests`.
- `src/OpenTelemetry.Extensions.Hosting/OpenTelemetry.Extensions.Hosting.csproj`
  (line ~19) -> `OpenTelemetry.Extensions.Hosting.Tests`.

No separate `Properties/AssemblyInfo.cs` `InternalsVisibleTo` declarations
observed; declarations live in the csproj `<ItemGroup>` sections.

### 4.H  Integration-test infrastructure

- `MockCollectorIntegrationTests.cs`: AspNetCore test server hosting HTTP/gRPC
  collector endpoint; used to verify export recovery and retry/status-code
  behaviour.
- `IntegrationTests.cs`: guarded by `SkipUnlessEnvVarFoundFact` on
  `OTEL_COLLECTOR_HOSTNAME`; runs against live collector.

---

## 5. xUnit naming convention survey

Counts are approximate (agent-derived from file scans) and pooled across the
three projects.

### 5.A  Dominant patterns

| Pattern | Approximate share | Example |
| --- | --- | --- |
| `Subject_Condition_Expected` (underscore-separated) | ~60-70 % | `BatchExportProcessorOptions_EnvironmentVariableOverride`, `OtlpExporterOptions_UsingIConfiguration`, `IsEnabled_ReturnsTrue_WhenCaCertificateFilePathProvided`, `UserAgentProductIdentifier_EmptyOrWhitespace_UsesDefaultUserAgent` |
| `Method_Condition_Expected` (underscore-separated verb-led) | ~15-20 % | `GetHeaders_NoOptionHeaders_ReturnsStandardHeaders`, `LoadClientCertificate_ThrowsFileNotFoundException_WhenCertificateFileDoesNotExist` |
| `FeatureDescriptionTest` (no underscores, suffix `Test`) | ~10-15 % | `ConfigureResourceTest`, `CircularReferenceTest`, `OptionReloadingTest`, `UseOtlpExporterDefaultTest` |
| `When<Condition><Outcome>` (narrative / Given-When-Then) | <5 % | `WhenOpenTelemetrySdkIsDisabledExceptionNotThrown` |
| `Test<Subject><Scenario>` (prefix `Test`) | <5 % | `TestSamplerSetFromConfiguration`, `TestAddOtlpExporter_NamedOptions`, `TestExemplarFilterSetFromConfiguration` |

### 5.B  Representative examples

1. `BatchExportProcessorOptions_Defaults`
2. `BatchExportProcessorOptions_EnvironmentVariableOverride`
3. `OtlpExporterOptions_UsingIConfiguration`
4. `OtlpExporterOptions_SetterOverridesEnvironmentVariable`
5. `IsEnabled_ReturnsTrue_WhenCaCertificateFilePathProvided`
6. `ConfigureResourceTest`
7. `UseOpenTelemetryOptionsOrderingTest`
8. `AddOpenTelemetry_WithTracing_HostConfigurationHonoredTest`
9. `ReloadOfMetricsViaIConfigurationWithExportCleanupTest`
10. `WhenOpenTelemetrySdkIsDisabledExceptionNotThrown`

### 5.C  Observations (facts only)

- Theory tests are named once; per-`InlineData` scenarios are not encoded in
  method names.
- Mixed `Test` suffix and `Test` prefix conventions coexist across projects; no
  project appears to enforce a single style.
- Underscore-separated `Subject_Condition_Expected` is the most common, though
  it is not universal.
- Hosting-tests lean toward `AddOpenTelemetry_With<Signal>_<Scenario>Test` for
  the DI extension tests, providing a minor project-specific variant of the
  dominant underscore pattern.

---

## 6. Notes and caveats

- Tables were compiled from three parallel Explore agent sweeps. Per-method line
  numbers are inconsistent across sections (captured when the agent emitted
  them, omitted otherwise). Session 0b or downstream files that need precise
  line numbers should re-grep the file.
- The agent surveying `OpenTelemetry.Tests` catalogued the largest
  config-adjacent files in its primary tables but may not have enumerated every
  borderline case under `Concurrency/`, `Propagation/` subfolders. These were
  excluded after spot-check as containing no config-adjacent methods beyond the
  `SkipUnlessEnvVarFoundFact` usage, which is isolation machinery rather than a
  config test.
- The OTLP agent's per-file tables omitted config-adjacent methods in
  `OtlpTraceExporterTests.cs`, `OtlpLogExporterTests.cs`, and
  `OtlpMetricsExporterTests.cs`; a manual enumeration was added in Sec.1.B for
  each. For `OtlpMetricsExporterTests`, a pair of Theory methods near lines
  734/765 exercise temporality-preference via env var and IConfiguration; the
  method identifiers were inferred from `InlineData` values and should be
  verified by a Session 1+ session that opens the file.
- One agent flagged the `Mtls*` methods in `OtlpExporterOptionsTests` as
  "unsafe" in its isolation audit. That is a mis-classification: the class has
  IDisposable-based class-level isolation (Sec.2.A) plus
  `[Collection("EnvVars")]` (Sec.2.C). The corrected fact is recorded here.
- `MetricExemplarTests.TestExemplarFilterSetFromConfiguration` uses
  `IConfiguration` (no env var) and is listed under direct observation; the
  "unsafe" note in the raw agent output referred to an adjacency concern, not a
  real env-var mutation.
- `gen_test_cert.ps1` / `gen_test_cert.sh` are present in the OTLP test project
  but were reported as not currently referenced by any test; confirm in Session
  0b.
- Process-isolation (child process, AssemblyLoadContext) is **absent** from the
  current test-infrastructure landscape; this is a fact for Session 0b to weigh,
  not a gap to recommend against here.
