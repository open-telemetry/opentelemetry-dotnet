# OtlpMtlsOptions - Configuration Test Coverage

Per-options-class file. Loads against
[`configuration-test-coverage.md`](../../configuration-test-coverage.md) and
the relevant rows of
[`existing-tests.md`](../existing-tests.md). Opinion lives in Sections 2
and 3; Section 1 is inventory only.

**TFM scope:** `#if NET` only. `OtlpMtlsOptions` and its consumer
`OtlpSecureHttpClientFactory` are compiled exclusively under `net8.0` and
later. Every recommendation in this file targets `net8.0+` test targets.
No `.NET Framework` or `.NET Standard 2.0/2.1` coverage is in scope.

## Source citations

- Type declaration (`OtlpMtlsOptions : OtlpTlsOptions`, `#if NET`) -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpMtlsOptions.cs:17`
- `ClientCertificatePath` property -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpMtlsOptions.cs:26`
- `ClientKeyPath` property -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpMtlsOptions.cs:35`
- `IsMtlsEnabled` computed property (`!string.IsNullOrWhiteSpace(ClientCertificatePath)`) -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpMtlsOptions.cs:44-45`
- `IsEnabled` computed property (`IsTlsEnabled || IsMtlsEnabled`) -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpMtlsOptions.cs:51`
- Parent class `OtlpTlsOptions` (supplies `CaCertificatePath`,
  `EnableCertificateChainValidation`, `IsTlsEnabled`, virtual `IsMtlsEnabled`) -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpTlsOptions.cs:17-48`
- `OtlpExporterOptions.MtlsOptions` property (internal; `#if NET`) -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpExporterOptions.cs:183`
- Env-var reads (populated into `MtlsOptions` by `ApplyMtlsConfiguration`) -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpExporterOptions.cs:313-337`
  - `OTEL_EXPORTER_OTLP_CERTIFICATE` -> `MtlsOptions.CaCertificatePath`
    (constant at
    `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/OtlpSpecConfigDefinitions.cs:37`)
  - `OTEL_EXPORTER_OTLP_CLIENT_CERTIFICATE` -> `MtlsOptions.ClientCertificatePath`
    (constant at
    `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/OtlpSpecConfigDefinitions.cs:39`)
  - `OTEL_EXPORTER_OTLP_CLIENT_KEY` -> `MtlsOptions.ClientKeyPath`
    (constant at
    `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/OtlpSpecConfigDefinitions.cs:38`)
- `ApplyMtlsConfiguration` private method (called from the main
  `OtlpExporterOptions` constructor at line 308) -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpExporterOptions.cs:313-337`

### Direct consumer sites

- `OtlpSecureHttpClientFactory.CreateSecureHttpClient` - primary consumer;
  reads `tlsOptions.CaCertificatePath`, `tlsOptions.EnableCertificateChainValidation`,
  pattern-matches `tlsOptions as OtlpMtlsOptions`, reads `IsMtlsEnabled`,
  `ClientCertificatePath`, `ClientKeyPath` -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/OtlpSecureHttpClientFactory.cs:23-115`
- `OtlpSecureHttpClientFactory.CreateMtlsHttpClient` (backward-compat wrapper,
  delegates to `CreateSecureHttpClient`) -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/OtlpSecureHttpClientFactory.cs:127-132`
- `OtlpCertificateManager.LoadCaCertificate` - reads from
  `tlsOptions.CaCertificatePath` path via caller -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/OtlpCertificateManager.cs:32-56`
- `OtlpCertificateManager.LoadClientCertificate` - reads from
  `mtlsOptions.ClientCertificatePath` and `mtlsOptions.ClientKeyPath` via caller -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/OtlpCertificateManager.cs:60+`

---

## 1. Existing coverage

Pulled from
[`existing-tests.md`](../existing-tests.md) (Sec.1.B). Inventory only.

Projects: `OTPT` = `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/`.

### 1.1 `OtlpMtlsOptionsTests.cs` (OTPT)

All 7 tests target `net8.0+` via `#if NET`. No env-var reads; no isolation
needed.

| File:method | Scenario summary | Observation mechanism | Env-var isolation |
| --- | --- | --- | --- |
| `OtlpMtlsOptionsTests.DefaultValues_AreValid` | All properties null/false by default; `IsEnabled` false | DirectProperty | Not env-var dependent |
| `OtlpMtlsOptionsTests.Properties_CanBeSet` | All four properties round-trip; `IsEnabled` true after setting `ClientCertificatePath` | DirectProperty | Not env-var dependent |
| `OtlpMtlsOptionsTests.IsEnabled_ReturnsFalse_WhenNoClientCertificateProvided` | `IsEnabled` false when nothing set | DirectProperty | Not env-var dependent |
| `OtlpMtlsOptionsTests.IsEnabled_ReturnsTrue_WhenCaCertificateFilePathProvided` | `IsEnabled` true via CA cert only (TLS path) | DirectProperty | Not env-var dependent |
| `OtlpMtlsOptionsTests.IsEnabled_ReturnsFalse_WhenCaCertificateFilePathIsEmpty` | Empty/whitespace `CaCertificatePath` -> `IsEnabled` false (Theory) | DirectProperty | Not env-var dependent |
| `OtlpMtlsOptionsTests.IsEnabled_ReturnsTrue_WhenClientCertificateFilePathProvided` | `IsEnabled` true via client cert path | DirectProperty | Not env-var dependent |
| `OtlpMtlsOptionsTests.IsEnabled_ReturnsFalse_WhenClientCertificateFilePathIsEmpty` | Empty/whitespace `ClientCertificatePath` -> `IsEnabled` false (Theory) | DirectProperty | Not env-var dependent |

### 1.2 `OtlpTlsOptionsTests.cs` (OTPT) - rows exercising `OtlpMtlsOptions`

`OtlpTlsOptionsTests.cs` is compiled under `#if NET`. Tests that directly
construct `OtlpMtlsOptions` or invoke `OtlpSecureHttpClientFactory` with
mTLS options are listed here.

| File:method | Scenario summary | Observation mechanism | Env-var isolation |
| --- | --- | --- | --- |
| `OtlpTlsOptionsTests.OtlpMtlsOptions_IsMtlsEnabled_ReturnsTrue_WhenClientCertificateProvided` | `IsMtlsEnabled` true when `ClientCertificatePath` set | DirectProperty | Not env-var dependent |
| `OtlpTlsOptionsTests.OtlpMtlsOptions_IsMtlsEnabled_ReturnsFalse_WhenOnlyCaCertificateProvided` | CA cert alone does not constitute mTLS; `IsTlsEnabled` true | DirectProperty | Not env-var dependent |
| `OtlpTlsOptionsTests.OtlpSecureHttpClientFactory_CreatesClient_WithMtlsClientCertificate` | `CreateSecureHttpClient(OtlpMtlsOptions)` succeeds with PFX client cert (SkipIfCryptoNotSupported) | Behavioural side-effect (non-null `HttpClient`) | Not env-var dependent |
| `OtlpTlsOptionsTests.OtlpSecureHttpClientFactory_ThrowsArgumentNullException_WhenOptionsIsNull` | Null argument throws | Exception | Not env-var dependent |
| `OtlpTlsOptionsTests.OtlpSecureHttpClientFactory_ThrowsInvalidOperationException_WhenTlsNotEnabled` | `OtlpTlsOptions` with no paths throws | Exception | Not env-var dependent |

### 1.3 `OtlpSecureHttpClientFactoryTests.cs` (OTPT) - rows exercising `OtlpMtlsOptions`

The actual test file contains 13 `[Fact]` methods, all compiled under
`#if NET`. The existing-tests.md inventory was produced before several of
these methods were added; actual names are taken from the source file
directly. Rows limited to those that exercise `OtlpMtlsOptions` properties
as consumer inputs.

| File:method | Scenario summary | Observation mechanism | Env-var isolation |
| --- | --- | --- | --- |
| `OtlpSecureHttpClientFactoryTests.CreateHttpClient_ThrowsInvalidOperationException_WhenMtlsIsDisabled` | `OtlpMtlsOptions` with no paths -> `InvalidOperationException` | Exception | Not env-var dependent |
| `OtlpSecureHttpClientFactoryTests.CreateHttpClient_ThrowsFileNotFoundException_WhenCertificateFileDoesNotExist` | Non-existent `ClientCertificatePath` -> `FileNotFoundException` | Exception | Not env-var dependent |
| `OtlpSecureHttpClientFactoryTests.CreateHttpClient_ConfiguresClientCertificate_WhenValidCertificateProvided` | Valid PFX cert file -> `HttpClientHandler.ClientCertificates` non-empty | Reflection (`HttpMessageInvoker._handler`) | Not env-var dependent |
| `OtlpSecureHttpClientFactoryTests.CreateHttpClient_ConfiguresServerCertificateValidation_WhenCaCertificatesProvided` | `CaCertificatePath` -> `ServerCertificateCustomValidationCallback` set (SkipIfCryptoNotSupported) | Reflection (`_handler`) | Not env-var dependent |
| `OtlpSecureHttpClientFactoryTests.CreateHttpClient_ConfiguresServerValidation_WithCaOnly` | CA-only path -> empty `ClientCertificates`, non-null callback (SkipIfCryptoNotSupported) | Reflection (`_handler`) | Not env-var dependent |
| `OtlpSecureHttpClientFactoryTests.CreateHttpClient_InvokesServerValidationCallbackAfterFactoryReturns` | Callback invoked post-factory with custom CA returns true (SkipIfCryptoNotSupported) | Reflection + Behavioural | Not env-var dependent |
| `OtlpSecureHttpClientFactoryTests.ValidateServerCertificate_ReturnsTrue_WhenNoSslPolicyErrors` | `OtlpCertificateManager.ValidateServerCertificate` with no SSL errors | DirectProperty (return value) | Not env-var dependent |
| `OtlpSecureHttpClientFactoryTests.ValidateServerCertificate_ReturnsFalse_WhenNameMismatch` | Name-mismatch policy error -> false | DirectProperty | Not env-var dependent |
| `OtlpSecureHttpClientFactoryTests.ValidateServerCertificate_ReturnsTrue_WithProvidedCa` | Chain-errors with matching CA -> true; chain tail matches | DirectProperty | Not env-var dependent |
| `OtlpSecureHttpClientFactoryTests.ValidateServerCertificate_ReturnsFalse_WhenCaDoesNotMatch` | Chain-errors with wrong CA -> false | DirectProperty | Not env-var dependent |
| `OtlpSecureHttpClientFactoryTests.ValidateServerCertificate_ReturnsFalse_WhenCaDoesNotMatch_EvenIfSslPolicyErrorsNone` | No SSL errors but CA mismatch -> false | DirectProperty | Not env-var dependent |
| `OtlpSecureHttpClientFactoryTests.ValidateCertificateChain_ReturnsFalseForExpiredCertificate` | Expired cert fails `ValidateCertificateChain` | DirectProperty | Not env-var dependent |
| `OtlpSecureHttpClientFactoryTests.CreateSecureHttpClient_ThrowsArgumentNullException_WhenOptionsIsNull` | Null `tlsOptions` argument -> `ArgumentNullException` with `paramName = "tlsOptions"` | Exception | Not env-var dependent |

### 1.4 `OtlpExporterOptionsTests.cs` (OTPT) - mTLS env-var rows

These tests construct `OtlpExporterOptions` with env vars set and then
assert on `MtlsOptions` properties. Class-level `IDisposable`
snapshot/restore plus `[Collection("EnvVars")]`.

| File:method | Scenario summary | Observation mechanism | Env-var isolation |
| --- | --- | --- | --- |
| `OtlpExporterOptionsTests.OtlpExporterOptions_MtlsEnvironmentVariables` | `OTEL_EXPORTER_OTLP_CERTIFICATE` -> `MtlsOptions.CaCertificatePath` | DirectProperty on `MtlsOptions` | Class-level snapshot/restore + `[Collection("EnvVars")]` |
| `OtlpExporterOptionsTests.OtlpExporterOptions_MtlsEnvironmentVariables_ClientCertificate` | `OTEL_EXPORTER_OTLP_CLIENT_CERTIFICATE` + `OTEL_EXPORTER_OTLP_CLIENT_KEY` -> paths stored | DirectProperty on `MtlsOptions` | Class-level snapshot/restore + `[Collection("EnvVars")]` |
| `OtlpExporterOptionsTests.OtlpExporterOptions_MtlsEnvironmentVariables_AllCertificates` | All three env vars -> all three paths stored | DirectProperty on `MtlsOptions` | Class-level snapshot/restore + `[Collection("EnvVars")]` |
| `OtlpExporterOptionsTests.OtlpExporterOptions_MtlsEnvironmentVariables_NoEnvironmentVariables` | No env vars -> `MtlsOptions` is null | DirectProperty on `MtlsOptions` | Class-level snapshot/restore + `[Collection("EnvVars")]` |
| `OtlpExporterOptionsTests.OtlpExporterOptions_MtlsEnvironmentVariables_UsingIConfiguration` | mTLS paths via `IConfiguration` (appsettings-shaped) | DirectProperty on `MtlsOptions` | Class-level snapshot/restore + `[Collection("EnvVars")]` |

---

## 2. Scenario checklist and gap analysis

Status: **covered**, **partial**, or **missing**. "Currently tested by"
cites Section 1 tests or dashes.

### 2.1 Constructor env-var reads (per property)

`ApplyMtlsConfiguration` is called from the `OtlpExporterOptions`
constructor at line 308. All three reads use
`IConfiguration.TryGetStringValue`; the configuration is the same
env-var-backed `IConfiguration` built inside the `OtlpExporterOptions`
parameterless constructor.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `OTEL_EXPORTER_OTLP_CERTIFICATE` -> `MtlsOptions.CaCertificatePath` | `OtlpExporterOptions_MtlsEnvironmentVariables`, `_AllCertificates` | String stored verbatim; `MtlsOptions` lazily created | covered |
| `OTEL_EXPORTER_OTLP_CLIENT_CERTIFICATE` -> `MtlsOptions.ClientCertificatePath` | `OtlpExporterOptions_MtlsEnvironmentVariables_ClientCertificate`, `_AllCertificates` | String stored verbatim; `MtlsOptions` lazily created | covered |
| `OTEL_EXPORTER_OTLP_CLIENT_KEY` -> `MtlsOptions.ClientKeyPath` | `OtlpExporterOptions_MtlsEnvironmentVariables_ClientCertificate`, `_AllCertificates` | String stored verbatim | covered |
| No env vars set -> `MtlsOptions` remains null | `OtlpExporterOptions_MtlsEnvironmentVariables_NoEnvironmentVariables` | `MtlsOptions` is null; `IsEnabled` not reachable | covered |
| Env var set to empty string -> stored or ignored | - | `TryGetStringValue` succeeds for empty strings; empty stored into `CaCertificatePath` (IsEnabled false via IsTlsEnabled) | missing |
| `IConfiguration` binding (appsettings keys) | `OtlpExporterOptions_MtlsEnvironmentVariables_UsingIConfiguration` | Same `ApplyMtlsConfiguration` path; key names match env var names | covered |

Notes: The three env-var reads use `IConfiguration.TryGetStringValue` (no
URI parsing, no integer parsing). There is no validation of whether the
path string is non-empty, absolute, or pointing to an existing file at
construction time. File existence is checked only inside
`OtlpCertificateManager.LoadCaCertificate` / `LoadClientCertificate`
at client-build time.

### 2.2 Priority order

`OtlpMtlsOptions` is a child object of `OtlpExporterOptions.MtlsOptions`.
It has no independent DI registration and no `Configure<T>` delegation.
Priority ordering applies at the `OtlpExporterOptions` level, not here.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Programmatic setter beats env var (set `MtlsOptions.CaCertificatePath` after construction) | - | Property can be overwritten; no guard; env-var value is simply replaced | missing |
| Env var beats no-config default (null) | `OtlpExporterOptions_MtlsEnvironmentVariables` | Env var wins over null default | covered |
| `IConfiguration` binding applies same as env var | `OtlpExporterOptions_MtlsEnvironmentVariables_UsingIConfiguration` | Same path; no precedence difference | covered |

### 2.3 Default-state baseline

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| All `OtlpMtlsOptions` properties null, `IsEnabled` false, `IsMtlsEnabled` false, `IsTlsEnabled` false, `EnableCertificateChainValidation` true | `OtlpMtlsOptionsTests.DefaultValues_AreValid` | Confirmed | covered |
| `OtlpExporterOptions.MtlsOptions` null when no env vars | `OtlpExporterOptions_MtlsEnvironmentVariables_NoEnvironmentVariables` | `MtlsOptions` is null; not a new `OtlpMtlsOptions()` | covered |
| Stable snapshot of all four properties at default state | - | Not snapshotted | missing (candidate for snapshot pilot) |

### 2.4 Named options

N/A - child of `OtlpExporterOptions`. `OtlpMtlsOptions` has no independent
DI registration and is not associated with any named-options instance.
The named-options story for OTLP lives in
[`otlp-exporter-options.md`](otlp-exporter-options.md).

### 2.5 Invalid-input characterisation

Pin current silent-accept behaviour. All rows marked missing are expected
to change under Issue 1 (add `IValidateOptions<T>` and `ValidateOnStart`
for all options classes).

| Property | Invalid input | Current behaviour | Currently tested by | Status |
| --- | --- | --- | --- | --- |
| `CaCertificatePath` | Non-existent path stored via env var | Stored silently; `FileNotFoundException` thrown later at `OtlpCertificateManager.LoadCaCertificate` | `OtlpCertificateManager_LoadCaCertificate_ThrowsFileNotFoundException` (consumer-side) | covered (at consumer) |
| `CaCertificatePath` | Empty string from env var | Stored as empty; `IsTlsEnabled` returns false (whitespace check) | - | missing (no test pins the empty-env-var -> `IsTlsEnabled` false path) |
| `CaCertificatePath` | Path to malformed (non-PEM) file | `InvalidOperationException` at `LoadCaCertificate` | `OtlpCertificateManager` tests (`LoadCaCertificate_ThrowsInvalidOperationException_WhenTrustStoreFileIsEmpty` in `OtlpCertificateManagerTests.cs`) | covered (at consumer) |
| `ClientCertificatePath` | Non-existent path | `FileNotFoundException` at `OtlpCertificateManager.LoadClientCertificate` | `CreateHttpClient_ThrowsFileNotFoundException_WhenCertificateFileDoesNotExist`, `LoadClientCertificate_ThrowsFileNotFoundException_WhenCertificateFileDoesNotExist` | covered (at consumer) |
| `ClientCertificatePath` | Empty string from env var | Stored as empty; `IsMtlsEnabled` returns false (whitespace check) | - | missing |
| `ClientCertificatePath` | Path to malformed cert file | `InvalidOperationException` at `LoadClientCertificate` | `LoadClientCertificate_ThrowsInvalidOperationException_WhenCertificateFileIsEmpty` | covered (at consumer) |
| `ClientKeyPath` | Non-existent path | `FileNotFoundException` at `LoadClientCertificate` | `LoadClientCertificate_ThrowsFileNotFoundException_WhenPrivateKeyFileDoesNotExist` | covered (at consumer) |
| `ClientKeyPath` | Null (cert without key) | `LoadClientCertificate` is called with `null` key path; PKCS#12 path taken | `LoadClientCertificate_LoadsFromSeparateFiles` exercises the key-present path; null-key path is a separate code branch | partial (null key path not pinned independently) |
| `EnableCertificateChainValidation` | `false` | Skips `ValidateCertificateChain` calls in `CreateSecureHttpClient` | Several `OtlpSecureHttpClientFactoryTests` pass `EnableCertificateChainValidation = false` to avoid platform issues | covered (by side-effect) |
| `EnableCertificateChainValidation` | `true` (default) with expired cert | `ValidateCertificateChain` returns false | `ValidateCertificateChain_ReturnsFalseForExpiredCertificate` | covered |

### 2.6 Reload no-op baseline

`OtlpMtlsOptions` is a child of `OtlpExporterOptions`. It inherits the
same no-reload characteristic: env-var reads happen once inside the
`OtlpExporterOptions` constructor; `MtlsOptions` is populated at
construction and never updated. `IOptionsMonitor<OtlpExporterOptions>` does
not re-run `ApplyMtlsConfiguration` on `IConfigurationRoot.Reload()`.

The reload no-op baseline for the parent class is covered in
[`otlp-exporter-options.md`](otlp-exporter-options.md) Section 3.4. No
independent reload scenario is required for `OtlpMtlsOptions` beyond what
is recorded there.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `IConfigurationRoot.Reload()` does not update `MtlsOptions` paths on a live exporter | - | Not tested; behaviour mirrors the parent class reload no-op | missing (see `otlp-exporter-options.md` Section 3.4 for parent coverage) |

### 2.7 Consumer-observed effects

Behaviours only visible inside `OtlpSecureHttpClientFactory`.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `CaCertificatePath` set -> `ServerCertificateCustomValidationCallback` installed on `HttpClientHandler` | `CreateHttpClient_ConfiguresServerCertificateValidation_WhenCaCertificatesProvided`, `CreateHttpClient_ConfiguresServerValidation_WithCaOnly`, `CreateHttpClient_InvokesServerValidationCallbackAfterFactoryReturns` | Callback reads `CaCertificateData` captured at factory-time; validates with `OtlpCertificateManager.ValidateServerCertificate` | covered (via reflection on `_handler`) |
| `ClientCertificatePath` set -> cert added to `HttpClientHandler.ClientCertificates` | `CreateHttpClient_ConfiguresClientCertificate_WhenValidCertificateProvided`, `OtlpSecureHttpClientFactory_CreatesClient_WithMtlsClientCertificate` | Client cert loaded and added to `ClientCertificates` | covered (via reflection on `_handler`) |
| `ClientKeyPath` null -> PKCS#12-format single-file load path taken | `LoadClientCertificate_LoadsFromSeparateFiles` (tests the key-present path) | No test pins the null-key (single-file PFX) load path end-to-end | partial |
| `ClientKeyPath` set -> separate PEM cert + PEM key load path | `CreateSecureHttpClient_ConfiguresClientCertificateChain` in existing-tests.md (but method not found in actual file at time of audit) | `OtlpCertificateManager.LoadClientCertificate(certPath, keyPath)` called | partial (existing-tests.md notes a test that was not found in the actual file; see Notes) |
| `EnableCertificateChainValidation = true` -> `ValidateCertificateChain` called for both CA and client certs | No factory-level test pins this path with `EnableCertificateChainValidation = true` and valid certs | `CreateSecureHttpClient` calls `ValidateCertificateChain` for each loaded cert when flag is true | missing |
| `EventSource` logging: `MtlsConfigurationEnabled` emitted when client cert loaded | - | `OpenTelemetryProtocolExporterEventSource.Log.MtlsConfigurationEnabled(clientCert.Subject)` called at line 74 of factory | missing |
| `EventSource` logging: `CaCertificateConfigured` emitted when CA-only path taken | - | `OpenTelemetryProtocolExporterEventSource.Log.CaCertificateConfigured(caCert.Subject)` called at line 79 of factory | missing |
| `EventSource` logging: `SecureHttpClientCreationFailed` emitted on exception | - | Called in `catch` at line 106 of factory | missing |

---

## 3. Recommendations

One recommendation per gap. Each targets a reviewable unit. Test names
follow the `Subject_Condition_Expected` convention (dominant style per
Session 0a Sec.5.A). Target location is the existing test file unless
noted. Tier labels per entry-doc Section 3. `#if NET` guard required on
every recommended test.

### 3.1 Invalid-input characterisation (guards Issue 1)

1. **`OtlpMtlsOptions_CaCertificatePath_EmptyString_IsTlsEnabledFalse`**
   (new; `OtlpMtlsOptionsTests.cs`).
   - Tier 1. Mechanism: DirectProperty. Constructs `OtlpMtlsOptions` with
     `CaCertificatePath = ""` and asserts `IsTlsEnabled == false`,
     `IsEnabled == false`.
   - Guards Issue 1.
   - Code-comment hint: "BASELINE: pins that empty-string path is treated
     as absent. Expected to change under Issue 1 (IValidateOptions<T>
     adds an explicit empty-string rejection)."
   - Risk vs reward: trivial effort; closes a boundary-condition gap in
     the `IsNullOrWhiteSpace` logic.

2. **`OtlpMtlsOptions_ClientCertificatePath_EmptyString_IsMtlsEnabledFalse`**
   (new; `OtlpMtlsOptionsTests.cs`).
   - Tier 1. Mechanism: DirectProperty. Mirrors recommendation 3.1.1 for
     `ClientCertificatePath`.
   - Guards Issue 1.
   - Same code-comment hint and risk-vs-reward as 3.1.1.

3. **`OtlpExporterOptions_MtlsEnvVar_EmptyString_MtlsOptionsStoredButDisabled`**
   (new; `OtlpExporterOptionsTests.cs`).
   - Tier 2. Mechanism: DirectProperty on `MtlsOptions`. Sets
     `OTEL_EXPORTER_OTLP_CERTIFICATE` to `""` and constructs
     `OtlpExporterOptions`; asserts `MtlsOptions` is non-null (empty
     string does trigger the `TryGetStringValue` branch), and
     `MtlsOptions.IsEnabled == false`.
   - Guards Issue 1. Isolation: class-level `IDisposable` +
     `[Collection("EnvVars")]`.
   - Code-comment hint: "BASELINE: pins silent accept of empty env-var
     value. Expected to change under Issue 1."
   - Risk vs reward: low effort; pins a boundary that affects whether
     Issue 1 validation must handle empty paths arriving from env vars.

4. **`OtlpMtlsOptions_ClientKeyPath_Null_LoadsFromSinglePfxFile`**
   (new; `OtlpSecureHttpClientFactoryTests.cs`).
   - Tier 2. Mechanism: Behavioural side-effect (non-null `HttpClient`
     returned; `SkipTestIfCryptoNotSupported`). Constructs a PFX
     temporary file, sets `ClientCertificatePath` to that file and
     leaves `ClientKeyPath` null; asserts `CreateSecureHttpClient`
     succeeds. Pins the "single-file PFX" load path distinct from the
     separate cert+key path.
   - Guards Issue 1.
   - Code-comment hint: "BASELINE: pins null-key -> PFX single-file path
     in OtlpCertificateManager.LoadClientCertificate."
   - Risk vs reward: moderate effort (needs a valid PFX temp file);
     closes the partial-coverage gap on the null-key branch.

5. **`OtlpSecureHttpClientFactory_EnableCertificateChainValidation_True_ValidatesChain`**
   (new; `OtlpSecureHttpClientFactoryTests.cs`).
   - Tier 2. Mechanism: Exception (expects `ValidateCertificateChain`
     to be called; use an expired cert so the call returns false and
     the factory throws, or assert via `ValidateCertificateChain_ReturnsFalseForExpiredCertificate`
     inline). Set `EnableCertificateChainValidation = true` (the
     default) with an expired CA cert; assert `InvalidOperationException`
     or `false` from chain validation.
   - Guards Issue 1.
   - Code-comment hint: "BASELINE: pins that EnableCertificateChainValidation
     = true (default) triggers the ValidateCertificateChain call path."
   - Risk vs reward: low-to-medium effort; pins the default chain-
     validation flow that all CA-cert scenarios rely on.

### 3.2 Priority order

1. **`OtlpExporterOptions_MtlsOptions_ProgrammaticSetter_BeatsEnvVar`**
   (new; `OtlpExporterOptionsTests.cs`).
   - Tier 2. Mechanism: DirectProperty on `MtlsOptions`. Sets
     `OTEL_EXPORTER_OTLP_CERTIFICATE` to path A; constructs
     `OtlpExporterOptions`; then overwrites `MtlsOptions.CaCertificatePath`
     to path B; asserts path B is stored.
   - Guards Issue 1 (validation must not revalidate old value after
     programmatic override).
   - Code-comment hint: "BASELINE: pins that MtlsOptions properties are
     mutable after construction; env-var value can be overwritten."
   - Risk vs reward: low effort; explicitly documents the mutability
     contract.

### 3.3 Consumer-observed effects (missing EventSource coverage)

These three recommendations are the only missing consumer-level gaps not
already addressed by existing tests.

1. **`OtlpSecureHttpClientFactory_ClientCert_EmitsMtlsConfigurationEnabled`**
   (new; `OtlpSecureHttpClientFactoryTests.cs`).
   - Tier 2. Mechanism: EventSource (`InMemoryEventListener` from
     `test/OpenTelemetry.Tests/Shared/InMemoryEventListener.cs`; note
     this helper is not yet linked into the OTLP test project - see
     Prerequisites). Wraps `CreateSecureHttpClient` with a valid client
     cert; asserts `MtlsConfigurationEnabled` event is emitted.
   - Guards Issue 6 (diagnostic logging for config paths), Issue 1.
   - Code-comment hint: "BASELINE: pins EventSource emission on
     successful mTLS client-cert load."
   - Risk vs reward: medium effort (requires linking
     `InMemoryEventListener` or duplicating it); high value for Issue 6
     delta visibility.

2. **`OtlpSecureHttpClientFactory_CaCertOnly_EmitsCaCertificateConfigured`**
   (new; `OtlpSecureHttpClientFactoryTests.cs`). Tier 2. Same mechanism
   as 3.3.1 but with CA-only options; asserts
   `CaCertificateConfigured` event. Guards Issues 6, 1.

3. **`OtlpSecureHttpClientFactory_InvalidCert_EmitsSecureHttpClientCreationFailed`**
   (new; `OtlpSecureHttpClientFactoryTests.cs`). Tier 2. Passes a
   non-existent path for a cert; wraps the expected exception assertion
   inside the event listener; asserts `SecureHttpClientCreationFailed`
   event is emitted before the exception propagates. Guards Issues 6, 1.
   - Code-comment hint: "BASELINE: pins EventSource emission on creation
     failure. Expected to remain under Issue 6 (which adds *more*
     logging, not removal)."

### 3.4 Reload no-op baseline (cross-reference)

The `OtlpMtlsOptions`-specific reload scenario (Section 2.6) is covered
by the parent-class reload no-op tests recommended in
[`otlp-exporter-options.md`](otlp-exporter-options.md) Section 3.4. No
additional test is recommended here. If a maintainer adds a dedicated
`OtlpMtlsOptions`-targeted reload test, the shared pathway spec at
[`../pathways/reload-no-op-baseline.md`](../pathways/reload-no-op-baseline.md)
should be consulted.

### 3.5 Default-state snapshot (pilot-dependent)

1. **`OtlpMtlsOptions_Default_Snapshot`** (new; `OtlpMtlsOptionsTests.cs`
   or `Snapshots/` subfolder if the pilot establishes one).
   - Tier 1. Mechanism: Snapshot (library choice per
     [entry doc Appendix A](../../configuration-test-coverage.md#appendix-a---snapshot-library-comparison)).
     Constructs a default `OtlpMtlsOptions()` and snapshots all
     properties.
   - Guards Issue 1 (any additive property change shows as a snapshot
     diff).
   - Code-comment hint: "BASELINE: pins whole-options default shape.
     Snapshot update expected on any additive property change."
   - Risk vs reward: low per-test cost once library chosen; surface is
     small (four properties plus two computed booleans).

### Prerequisites and dependencies

- Recommendations 3.3.1-3.3.3 depend on `InMemoryEventListener` being
  accessible in the OTLP test project. Currently it lives in
  `test/OpenTelemetry.Tests/Shared/` and is linked into
  `test/OpenTelemetry.Extensions.Hosting.Tests/` but not into the OTLP
  test project (Session 0a Sec.4.D). A `<Compile Include ... Link>`
  entry in the OTLP test project's csproj is a prerequisite; this is a
  test-infrastructure change only (no `src/` edit).
- Recommendation 3.1.3 (empty env-var test) inherits the existing env-var
  isolation pattern (`IDisposable` snapshot/restore + `[Collection("EnvVars")]`)
  from `OtlpExporterOptionsTests`. No new pattern is required.
- Recommendation 3.5 depends on the snapshot-library selection
  ([entry doc Appendix A](../../configuration-test-coverage.md#appendix-a---snapshot-library-comparison)).

---

## Guards issues

This file specifies baseline tests that guard the following entries in
[`../../configuration-proposed-issues.md`](../../configuration-proposed-issues.md):

- **Issue 1** - Add `IValidateOptions<T>` for reload protection (no `ValidateOnStart`; deferred) for all
  options classes. Guarded by: Sections 3.1 (invalid-input
  characterisation), 3.2 (priority order), 3.5 (snapshot).
- **Issue 6** - Add diagnostic logging for `RegisterOptionsFactory` silent
  skip. Guarded by: Section 3.3 (EventSource emission at client-build
  time for mTLS success and failure paths).
