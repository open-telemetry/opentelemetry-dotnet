# OtlpTlsOptions - Configuration Test Coverage

Per-options-class file. Loads against
[`configuration-test-coverage.md`](../../configuration-test-coverage.md) and
the relevant rows of
[`existing-tests.md`](../existing-tests.md). Opinion lives in Sections 2
and 3; Section 1 is inventory only.

## Source citations

- Type declaration and class-level doc comment -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpTlsOptions.cs:17`
  (internal class, `#if NET` guarded).
- `CaCertificatePath` property (`string?`, default `null`) -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpTlsOptions.cs:26`.
- `EnableCertificateChainValidation` property (`bool`, default `true`) -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpTlsOptions.cs:32`.
- `IsTlsEnabled` computed property (`virtual bool`; returns
  `!string.IsNullOrWhiteSpace(CaCertificatePath)`) -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpTlsOptions.cs:37-38`.
- `IsMtlsEnabled` computed property (`virtual bool`; always returns `false`
  on the base class) -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpTlsOptions.cs:47`.
- `OtlpMtlsOptions` (derived sealed class; overrides both `IsMtlsEnabled` and
  exposes `ClientCertificatePath`, `ClientKeyPath`, `IsEnabled`) -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpMtlsOptions.cs:17`.
- Env-var reads that populate the concrete `OtlpMtlsOptions` instance wired
  into `OtlpExporterOptions.MtlsOptions`:
  - `OTEL_EXPORTER_OTLP_CERTIFICATE` -> `MtlsOptions.CaCertificatePath` -
    `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpExporterOptions.cs:318-322`.
  - `OTEL_EXPORTER_OTLP_CLIENT_CERTIFICATE` ->
    `MtlsOptions.ClientCertificatePath` -
    `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpExporterOptions.cs:325-329`.
  - `OTEL_EXPORTER_OTLP_CLIENT_KEY` -> `MtlsOptions.ClientKeyPath` -
    `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpExporterOptions.cs:332-336`.
  - All three are read inside
    `OtlpExporterOptions.ApplyMtlsConfiguration(IConfiguration)` -
    `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpExporterOptions.cs:313-337`.
  - Env-var name constants -
    `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/OtlpSpecConfigDefinitions.cs:37-39`.

Note: `OtlpTlsOptions` carries no env-var reads of its own. Env vars are
read in `OtlpExporterOptions.ApplyMtlsConfiguration` and stored into the
`OtlpMtlsOptions` instance (a subtype of `OtlpTlsOptions`) held at
`OtlpExporterOptions.MtlsOptions`. Direct construction of a bare
`OtlpTlsOptions` instance is only done in tests; production code always
uses `OtlpMtlsOptions`.

### Named options

N/A - child of `OtlpExporterOptions`. `OtlpTlsOptions` / `OtlpMtlsOptions`
are not registered independently with the DI options system; they are held
as a property (`MtlsOptions`) on the parent `OtlpExporterOptions`. Named-
options handling therefore belongs to the parent class. See
[`otlp-exporter-options.md`](otlp-exporter-options.md) Section 2.4.

### Direct consumer sites

- `OtlpExporterOptions.DefaultHttpClientFactory` lambda -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpExporterOptions.cs:71-92`.
  Checks `MtlsOptions?.IsEnabled == true`; if so calls
  `OtlpSecureHttpClientFactory.CreateSecureHttpClient(MtlsOptions, ...)` at
  line 79. This is the only production code path that constructs a TLS-secured
  `HttpClient` from `OtlpTlsOptions`.
- `OtlpSecureHttpClientFactory.CreateSecureHttpClient(OtlpTlsOptions, ...)` -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/OtlpSecureHttpClientFactory.cs:23-115`.
  Reads `IsTlsEnabled`, `IsMtlsEnabled`, `CaCertificatePath`,
  `EnableCertificateChainValidation`; casts to `OtlpMtlsOptions` to read
  client cert fields. Guards with `ArgumentNullException` (line 27) and
  `InvalidOperationException` when neither TLS nor mTLS is enabled (line 29-33).
- `OtlpSecureHttpClientFactory.CreateMtlsHttpClient(OtlpMtlsOptions, ...)` -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/OtlpSecureHttpClientFactory.cs:127-132`.
  Backward-compatibility shim; delegates to `CreateSecureHttpClient`.
- `OtlpCertificateManager.LoadCaCertificate` -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/OtlpCertificateManager.cs:32-56`.
  Called when `CaCertificatePath` is non-empty; throws `FileNotFoundException`
  for a missing file, `InvalidOperationException` for an unreadable file.
- `OtlpCertificateManager.ValidateCertificateChain` -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/OtlpCertificateManager.cs:159-211`.
  Called when `EnableCertificateChainValidation` is `true`.
- `OtlpCertificateManager.ValidateServerCertificate` -
  `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/Implementation/OtlpCertificateManager.cs:225-288`.
  Installed as the `ServerCertificateCustomValidationCallback` on the
  `HttpClientHandler` when a CA cert is provided.

---

## 1. Existing coverage

Pulled from [`existing-tests.md`](../existing-tests.md). Inventory only.

`File:method` is abbreviated to the test-method name where the file is
unambiguous. Project:

- `OTPT` = `test/OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests/`.

All tests are in the `OtlpTlsOptionsTests.cs` file unless noted. The
entire file is guarded `#if NET`.

### 1.1 `OtlpTlsOptionsTests.cs` (OTPT) - `OtlpTlsOptions` rows

| File:method | Scenario summary | Observation mechanism | Env-var isolation |
| --- | --- | --- | --- |
| `OtlpTlsOptionsTests.OtlpTlsOptions_IsTlsEnabled_ReturnsFalse_WhenNoCaCertificatePath` | `IsTlsEnabled` is `false` when `CaCertificatePath` is not set | DirectProperty | No env-var use |
| `OtlpTlsOptionsTests.OtlpTlsOptions_IsTlsEnabled_ReturnsTrue_WhenCaCertificatePathProvided` | `IsTlsEnabled` is `true` when `CaCertificatePath` is non-empty | DirectProperty | No env-var use |
| `OtlpTlsOptionsTests.OtlpTlsOptions_IsMtlsEnabled_ReturnsFalse_ByDefault` | `IsMtlsEnabled` returns `false` on the base class even when CA cert is set | DirectProperty | No env-var use |
| `OtlpTlsOptionsTests.OtlpSecureHttpClientFactory_CreatesClient_WithCaCertificateOnly` | Factory produces non-null `HttpClient` when only CA cert is set; chain validation disabled | InternalAccessor (static factory) | No env-var use |
| `OtlpTlsOptionsTests.OtlpSecureHttpClientFactory_ThrowsArgumentNullException_WhenOptionsIsNull` | Null `tlsOptions` arg throws | Exception | No env-var use |
| `OtlpTlsOptionsTests.OtlpSecureHttpClientFactory_ThrowsInvalidOperationException_WhenTlsNotEnabled` | Empty `OtlpTlsOptions` (no cert paths) throws | Exception | No env-var use |
| `OtlpTlsOptionsTests.OtlpCertificateManager_LoadCaCertificate_ThrowsFileNotFoundException` | Non-existent path throws `FileNotFoundException` | Exception | No env-var use |
| `OtlpTlsOptionsTests.OtlpCertificateManager_ValidateServerCertificate_ReturnsTrue_WhenNoSslPolicyErrors` | Server cert valid when `SslPolicyErrors.None` | InternalAccessor (static helper) | No env-var use |
| `OtlpTlsOptionsTests.OtlpCertificateManager_ValidateServerCertificate_ReturnsTrue_WithProvidedTrustedCert` | Server cert valid against custom CA when `RemoteCertificateChainErrors` only | InternalAccessor (static helper) | No env-var use |

### 1.2 `OtlpTlsOptionsTests.cs` (OTPT) - `OtlpMtlsOptions` rows (same file)

These tests exercise `OtlpMtlsOptions` (the derived class) but are
housed in `OtlpTlsOptionsTests.cs` and exercise base-class properties
via the subtype.

| File:method | Scenario summary | Observation mechanism | Env-var isolation |
| --- | --- | --- | --- |
| `OtlpTlsOptionsTests.OtlpMtlsOptions_IsMtlsEnabled_ReturnsTrue_WhenClientCertificateProvided` | `IsMtlsEnabled` is `true` when `ClientCertificatePath` is set | DirectProperty | No env-var use |
| `OtlpTlsOptionsTests.OtlpMtlsOptions_IsMtlsEnabled_ReturnsFalse_WhenOnlyCaCertificateProvided` | CA-only does not constitute mTLS; `IsTlsEnabled` still `true` | DirectProperty | No env-var use |
| `OtlpTlsOptionsTests.OtlpSecureHttpClientFactory_CreatesClient_WithMtlsClientCertificate` | Factory produces non-null `HttpClient` when client cert set; chain validation disabled | InternalAccessor (static factory) | No env-var use |

### 1.3 `OtlpExporterOptionsTests.cs` (OTPT) - env-var population of `MtlsOptions`

These tests exercise `OtlpTlsOptions`-related properties indirectly via
the `OtlpExporterOptions.MtlsOptions` field populated by
`ApplyMtlsConfiguration`.

| File:method | Scenario summary | Observation mechanism | Env-var isolation |
| --- | --- | --- | --- |
| `OtlpExporterOptionsTests.OtlpExporterOptions_MtlsEnvironmentVariables` | `OTEL_EXPORTER_OTLP_CERTIFICATE` populates `MtlsOptions.CaCertificatePath` | DirectProperty on `MtlsOptions` | Class-level `IDisposable` snapshot/restore + `[Collection]` |
| `OtlpExporterOptionsTests.OtlpExporterOptions_MtlsEnvironmentVariables_ClientCertificate` | `CLIENT_CERTIFICATE` + `CLIENT_KEY` populate the mTLS properties | DirectProperty on `MtlsOptions` | Class-level snapshot/restore |
| `OtlpExporterOptionsTests.OtlpExporterOptions_MtlsEnvironmentVariables_AllCertificates` | All three env vars populate all three paths | DirectProperty on `MtlsOptions` | Class-level snapshot/restore |
| `OtlpExporterOptionsTests.OtlpExporterOptions_MtlsEnvironmentVariables_NoEnvironmentVariables` | No env vars -> `MtlsOptions` is `null` | DirectProperty (`null` check) | Class-level snapshot/restore |
| `OtlpExporterOptionsTests.OtlpExporterOptions_MtlsEnvironmentVariables_UsingIConfiguration` | `IConfiguration` source populates `MtlsOptions` | DirectProperty on `MtlsOptions` | Class-level snapshot/restore |

### 1.4 Env-var isolation status for the class

`OtlpTlsOptionsTests.cs` contains no env-var reads or writes. No
snapshot/restore, no `[Collection]` attribute is present or needed. The
file is an isolated unit test file. Env-var tests for TLS/mTLS are housed
in `OtlpExporterOptionsTests.cs` (Section 1.3 above), which applies the
class-level `IDisposable` snapshot/restore pattern and is a member of the
`[Collection("EnvVars")]` collection.

---

## 2. Scenario checklist and gap analysis

Status column values: **covered**, **partial**, **missing**. "Currently
tested by" cites tests from Section 1 or dashes for none.

### 2.1 Property defaults (no env vars, no config)

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `CaCertificatePath` default is `null` | `OtlpTlsOptions_IsTlsEnabled_ReturnsFalse_WhenNoCaCertificatePath` (implicit) | `null` | partial (no explicit null assertion; the test only asserts `IsTlsEnabled == false`) |
| `EnableCertificateChainValidation` default is `true` | - | `true` | missing |
| `IsTlsEnabled` when `CaCertificatePath` is `null` | `OtlpTlsOptions_IsTlsEnabled_ReturnsFalse_WhenNoCaCertificatePath` | `false` | covered |
| `IsMtlsEnabled` on base class is always `false` | `OtlpTlsOptions_IsMtlsEnabled_ReturnsFalse_ByDefault` | `false` | covered |
| `IsTlsEnabled` when `CaCertificatePath` is whitespace-only | - | `false` (due to `IsNullOrWhiteSpace`) | missing |

### 2.2 Env-var reads (population of `MtlsOptions.CaCertificatePath`)

`OtlpTlsOptions.CaCertificatePath` is populated at the parent-class level
via `OtlpExporterOptions.ApplyMtlsConfiguration`. The three env vars are
`OTEL_EXPORTER_OTLP_CERTIFICATE` (CA cert), `OTEL_EXPORTER_OTLP_CLIENT_CERTIFICATE`
(client cert), and `OTEL_EXPORTER_OTLP_CLIENT_KEY` (client key). Only the
CA cert path is surfaced on `OtlpTlsOptions`; the other two are on the
derived `OtlpMtlsOptions`.

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `OTEL_EXPORTER_OTLP_CERTIFICATE` -> `MtlsOptions.CaCertificatePath` | `OtlpExporterOptions_MtlsEnvironmentVariables` | Path stored into `CaCertificatePath`; `MtlsOptions` created lazily | covered |
| No env vars -> `MtlsOptions` remains `null` | `OtlpExporterOptions_MtlsEnvironmentVariables_NoEnvironmentVariables` | `MtlsOptions == null` | covered |
| `IConfiguration` (appsettings-shaped) source populates `CaCertificatePath` | `OtlpExporterOptions_MtlsEnvironmentVariables_UsingIConfiguration` | Same as env-var path | covered |
| `OTEL_EXPORTER_OTLP_CERTIFICATE` set to empty string | - | `TryGetStringValue` succeeds with empty string; `MtlsOptions` created but `CaCertificatePath = ""`; `IsTlsEnabled` returns `false` (empty string is whitespace) | missing |
| Priority: programmatic set of `MtlsOptions.CaCertificatePath` after construction beats env var | - | Not tested; programmatic assignment wins because the object is already built | missing |

### 2.3 `IsTlsEnabled` / `IsMtlsEnabled` derived behaviour

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `IsTlsEnabled` returns `true` with non-empty path | `OtlpTlsOptions_IsTlsEnabled_ReturnsTrue_WhenCaCertificatePathProvided` | `true` | covered |
| `IsTlsEnabled` on `OtlpMtlsOptions` subtype with only CA cert | `OtlpMtlsOptions_IsMtlsEnabled_ReturnsFalse_WhenOnlyCaCertificateProvided` | `IsTlsEnabled == true`, `IsMtlsEnabled == false` | covered |
| `IsMtlsEnabled` on subtype with client cert set | `OtlpMtlsOptions_IsMtlsEnabled_ReturnsTrue_WhenClientCertificateProvided` | `true` | covered |
| `IsMtlsEnabled` on base class is `false` even when subtype overrides to `true` | `OtlpTlsOptions_IsMtlsEnabled_ReturnsFalse_ByDefault` | `false` on base; virtual dispatch on subtype returns `true` | covered |

### 2.4 `EnableCertificateChainValidation` consumer effect

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `EnableCertificateChainValidation = false` skips `ValidateCertificateChain` call in `CreateSecureHttpClient` | `OtlpSecureHttpClientFactory_CreatesClient_WithCaCertificateOnly` (passes `false`) | Chain validation skipped; client created | partial (the test passes with `false` explicitly to avoid needing a valid chain on CI; it does not separately pin the `true` path or assert that validation runs) |
| `EnableCertificateChainValidation = true` causes `ValidateCertificateChain` to be called | - | `ValidateCertificateChain` is called and may throw for self-signed certs without chain | missing |
| `EnableCertificateChainValidation` default value is `true` | - | Default is `true` (source line 32) | missing |

### 2.5 `OtlpSecureHttpClientFactory.CreateSecureHttpClient` guard conditions

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| Null `tlsOptions` throws `ArgumentNullException` | `OtlpSecureHttpClientFactory_ThrowsArgumentNullException_WhenOptionsIsNull` | Throws | covered |
| Empty `OtlpTlsOptions` (no paths) throws `InvalidOperationException` | `OtlpSecureHttpClientFactory_ThrowsInvalidOperationException_WhenTlsNotEnabled` | Throws with "must include at least a CA path or client certificate path" | covered |
| CA-cert-only path produces a client (no client cert required) | `OtlpSecureHttpClientFactory_CreatesClient_WithCaCertificateOnly` | Returns non-null `HttpClient` | covered (chain validation disabled in test) |
| mTLS path produces a client (client cert present) | `OtlpSecureHttpClientFactory_CreatesClient_WithMtlsClientCertificate` | Returns non-null `HttpClient` | covered (chain validation disabled in test) |
| `configureClient` action is invoked on the created client | - | `configureClient?.Invoke(client)` runs before return | missing |

### 2.6 `OtlpCertificateManager` consumer-observed effects

| Scenario | Currently tested by | Current behaviour | Status |
| --- | --- | --- | --- |
| `LoadCaCertificate` throws `FileNotFoundException` for non-existent path | `OtlpCertificateManager_LoadCaCertificate_ThrowsFileNotFoundException` | Throws | covered |
| `LoadCaCertificate` throws `InvalidOperationException` for unreadable / corrupt file | - | Throws `InvalidOperationException` wrapping the original exception | missing |
| `ValidateServerCertificate` returns `true` when `SslPolicyErrors.None` | `OtlpCertificateManager_ValidateServerCertificate_ReturnsTrue_WhenNoSslPolicyErrors` | Returns `true` | covered |
| `ValidateServerCertificate` returns `true` for cert issued by provided CA | `OtlpCertificateManager_ValidateServerCertificate_ReturnsTrue_WithProvidedTrustedCert` | Returns `true` and chain terminates at provided CA | covered |
| `ValidateServerCertificate` returns `false` when non-chain SSL errors present | - | `nonChainErrors != None` -> returns `false` | missing |
| `ValidateServerCertificate` returns `false` when server cert not issued by provided CA | - | Chain build fails, `isValid == false` -> returns `false` | missing |
| `ValidateServerCertificate` handles `null` `cert` or `chain` gracefully | - | Returns `false` (guard at top of callback lambda, line ~194) | missing |

### 2.7 Invalid-input characterisation (guards Issue 1)

Each property: what happens today when input is malformed or out of range?

| Property | Malformed input | Current behaviour | Currently tested by | Status |
| --- | --- | --- | --- | --- |
| `CaCertificatePath` | Empty string | `IsTlsEnabled` returns `false`; `IsNullOrWhiteSpace` guards | - | missing (no explicit test for the empty-string edge case) |
| `CaCertificatePath` | Whitespace-only string | `IsTlsEnabled` returns `false` | - | missing |
| `CaCertificatePath` | Non-existent file path | `LoadCaCertificate` throws `FileNotFoundException` at client-build time | `OtlpCertificateManager_LoadCaCertificate_ThrowsFileNotFoundException` | covered (at consumer) |
| `CaCertificatePath` | Valid path but not PEM | `LoadCaCertificate` catches `Exception` and throws `InvalidOperationException` | - | missing |
| `EnableCertificateChainValidation` | Programmatic `false` (not truly malformed; disables safety) | Chain validation skipped | `OtlpSecureHttpClientFactory_CreatesClient_WithCaCertificateOnly` (indirectly) | partial (covers the false path but no test pins what is skipped) |

All missing rows in 2.7 are expected to change under Issue 1
(`IValidateOptions<T>` + `ValidateOnStart`). Tests added here pin today's
silent-accept or deferred-throw behaviour so Issue 1 produces a visible
delta.

### 2.8 Reload no-op baseline

`OtlpTlsOptions` / `OtlpMtlsOptions` are not directly DI-registered and
have no `IOptionsMonitor<T>` path of their own. Reload behaviour is
inherited from the parent `OtlpExporterOptions`. No additional reload
scenarios are specific to `OtlpTlsOptions`; the reload tests recommended
in [`otlp-exporter-options.md`](otlp-exporter-options.md) Section 3.4
cover the full parent, which includes the `DefaultHttpClientFactory`
TLS path. No additional per-class reload recommendations are made here.

---

## 3. Recommendations

One bullet per gap. Each recommendation targets a reviewable PR unit.
Test name follows the dominant `Subject_Condition_Expected` convention
from the Session 0a naming survey. Target location is the existing test
file unless noted. Tier and observation-mechanism labels match entry-doc
Sections 3 and 2.

### 3.1 Property default baseline

1. **`OtlpTlsOptions_Defaults_EnableCertificateChainValidationIsTrue`**
   (new test in `OtlpTlsOptionsTests.cs`).
   - Tier 1. Mechanism: DirectProperty. Constructs `new OtlpTlsOptions()`
     and asserts `EnableCertificateChainValidation == true`.
   - Guards Issue 1.
   - Code-comment hint: "BASELINE: pins default. Expected to remain `true`
     unless Issue 1 introduces a validator that rejects insecure defaults."
   - Risk vs reward: trivial to write; pins a non-obvious default whose
     change would be a security regression.

2. **`OtlpTlsOptions_Defaults_CaCertificatePathIsNull`** (new; same file).
   - Tier 1. Mechanism: DirectProperty. Constructs `new OtlpTlsOptions()`
     and explicitly asserts `CaCertificatePath == null`.
   - Guards Issue 1.
   - Code-comment hint: "BASELINE: pins default null. No planned change."
   - Risk vs reward: very low effort; completes the default-state picture.

### 3.2 `IsTlsEnabled` whitespace edge case

1. **`OtlpTlsOptions_IsTlsEnabled_ReturnsFalse_WhenCaCertificatePathIsWhitespace`**
   (new; `OtlpTlsOptionsTests.cs`).
   - Tier 1. Mechanism: DirectProperty (Theory over `""`, `" "`, `"\t"`).
     Asserts `IsTlsEnabled == false` for each whitespace variant.
   - Guards Issue 1.
   - Code-comment hint: "BASELINE: whitespace treated as absent. Expected
     to change under Issue 1 if a validator is added that rejects
     empty paths earlier."
   - Risk vs reward: low cost; pins the `IsNullOrWhiteSpace` boundary
     that distinguishes TLS-on from TLS-off.

### 3.3 `EnableCertificateChainValidation` effect

1. **`OtlpTlsOptions_EnableCertificateChainValidation_False_SkipsValidation`**
   (new; `OtlpTlsOptionsTests.cs`). This test is a restructured version of
   the existing `OtlpSecureHttpClientFactory_CreatesClient_WithCaCertificateOnly`
   but with an explicit assertion that the client is produced without an
   exception (the existing test already provides this; the new test may add
   a mock or spy to confirm `ValidateCertificateChain` is not called).
   - Tier 1 (mock/spy variant using a subclass override) or Tier 1
     (behavioural - existing test already implies this without explicit
     confirmation). Mechanism: DirectProperty + Mock (subclass). Avoids
     filesystem I/O by mocking; or uses the existing `SkipTestIfCryptoNotSupported`
     wrapper if filesystem I/O remains.
   - Guards Issue 1.
   - Code-comment hint: "BASELINE: `false` bypasses chain validation. Pin
     before Issue 1 may add an opt-out warning or restriction."
   - Risk vs reward: low effort if it extends the existing test body; pins
     security-relevant behaviour.

2. **`OtlpTlsOptions_EnableCertificateChainValidation_Default_CallsValidation`**
   (new; `OtlpTlsOptionsTests.cs`). Requires a resolvable cert (or
   `SkipTestIfCryptoNotSupported` wrapper) to confirm that with
   `EnableCertificateChainValidation = true` (the default) the factory
   invokes `OtlpCertificateManager.ValidateCertificateChain`.
   - Tier 1 (with crypto skip guard). Mechanism: EventSource (observe
     log event `MtlsCertificateLoaded` / `MtlsCertificateChainValidated`
     emitted from `OtlpCertificateManager`) or InternalAccessor.
     EventSource approach is lowest brittleness; `InMemoryEventListener`
     infrastructure is already available in the OTLP test project (Session
     0a Sec.4.F).
   - Guards Issue 1.
   - Code-comment hint: "BASELINE: default chain validation path. Expected
     to remain the safe default; test delta expected if Issue 1 adds
     `ValidateOnStart` that calls this earlier."
   - Risk vs reward: moderate (requires a valid cert or EventSource
     instrumentation); high value because the default-`true` path is the
     security-critical one.

### 3.4 `OtlpSecureHttpClientFactory` gap closure

1. **`OtlpSecureHttpClientFactory_ConfigureClient_IsInvoked`** (new;
   `OtlpTlsOptionsTests.cs`).
   - Tier 1. Mechanism: Mock (pass a lambda `Action<HttpClient>` that sets
     a flag or captures the client; assert it was called). Constructs a
     minimal valid `OtlpTlsOptions` (with a real cert file or
     `SkipTestIfCryptoNotSupported`).
   - No issue guard (no planned change to this flow); code-comment:
     "BASELINE: pins `configureClient` invocation. No planned change."
   - Risk vs reward: low; pins the callback contract relied on by
     `OtlpExporterOptions.DefaultHttpClientFactory` when setting
     `client.Timeout`.

2. **`OtlpSecureHttpClientFactory_ThrowsInvalidOperationException_WhenTlsNotEnabled_OnSubtype`**
   (new or extended from existing; `OtlpTlsOptionsTests.cs`). Verify that
   a `new OtlpMtlsOptions()` with no paths set also throws (since
   `IsEnabled == false`).
   - Tier 1. Mechanism: Exception. Extends the already-covered null guard
     to cover the "enabled check fails on subtype" case.
   - Guards Issue 1.
   - Code-comment hint: "BASELINE: subtype with no paths also rejected at
     factory boundary. No planned change."
   - Risk vs reward: trivial one-line test; adds precision.

### 3.5 `OtlpCertificateManager` gap closure

1. **`OtlpCertificateManager_LoadCaCertificate_ThrowsInvalidOperationException_WhenFileIsUnreadable`**
   (new; `OtlpTlsOptionsTests.cs`).
   - Tier 1. Mechanism: Exception. Write an empty or corrupt temp file;
     assert `InvalidOperationException` is thrown by `LoadCaCertificate`.
   - Guards Issue 1.
   - Code-comment hint: "BASELINE: corrupt CA cert throws at load time.
     Expected to change under Issue 1 if path validation moves to
     `IValidateOptions<T>`."
   - Risk vs reward: low (temp file, no network); pins a deferred-throw
     that today only surfaces at client-build time.

2. **`OtlpCertificateManager_ValidateServerCertificate_ReturnsFalse_WhenNonChainSslErrors`**
   (new; `OtlpTlsOptionsTests.cs`).
   - Tier 1. Mechanism: InternalAccessor (call the `internal static`
     method directly with `SslPolicyErrors.RemoteCertificateNameMismatch`).
     Assert `false` returned.
   - Guards Issue 1.
   - Code-comment hint: "BASELINE: non-chain errors cause rejection.
     No planned change."
   - Risk vs reward: low; pins the fast-fail path for name-mismatch.

3. **`OtlpCertificateManager_ValidateServerCertificate_ReturnsFalse_WhenCertNotIssuedByProvidedCa`**
   (new; `OtlpTlsOptionsTests.cs`).
   - Tier 1. Mechanism: InternalAccessor. Construct two independent CA
     certs; validate a cert from CA1 against CA2. Assert `false`.
   - Guards Issue 1.
   - Code-comment hint: "BASELINE: wrong CA causes rejection.
     No planned change."
   - Risk vs reward: low; closes the most important negative case for
     custom CA trust.

4. **`OtlpCertificateManager_ValidateServerCertificate_ReturnsFalse_WhenCertOrChainNull`**
   (new; `OtlpTlsOptionsTests.cs`). Verify the null guard returns `false`
   rather than throwing.
   - Tier 1. Mechanism: InternalAccessor. Pass `null` for cert and/or
     chain into the internal static method.
   - Guards Issue 1.
   - Code-comment hint: "BASELINE: null inputs return false without
     throwing. No planned change."
   - Risk vs reward: trivial; closes a defensive null check.

### 3.6 Env-var edge case

1. **`OtlpExporterOptions_MtlsEnvironmentVariables_EmptyCertificateEnvVar_CreatesOptionsWithEmptyPath`**
   (new; `OtlpExporterOptionsTests.cs` to match the file that hosts all
   other mTLS env-var tests).
   - Tier 1. Mechanism: DirectProperty after constructor. Sets
     `OTEL_EXPORTER_OTLP_CERTIFICATE = ""` via class-level
     snapshot/restore. Asserts `MtlsOptions.CaCertificatePath == ""`
     and `IsTlsEnabled == false`.
   - Guards Issue 1.
   - Code-comment hint: "BASELINE: empty env var accepted at option level;
     TLS guard is at `IsTlsEnabled`. Expected to change under Issue 1
     if validation rejects empty paths early."
   - Risk vs reward: low; pins the subtle behaviour where an empty env
     var still materialises `MtlsOptions` but does not enable TLS.

### Prerequisites and dependencies

- 3.3.2 and 3.5.1 require file system access (temp cert file); both
  should use the existing `SkipTestIfCryptoNotSupported` wrapper already
  in the file.
- 3.3.2 (EventSource variant) depends on the `InMemoryEventListener`
  helper referenced in Session 0a Sec.4.F; if the helper is not already
  linked into the OTLP test project, the simpler behavioural assertion
  (no exception thrown) is the fallback.
- 3.6.1 depends on the class-level `IDisposable` env-var isolation pattern
  already used by `OtlpExporterOptionsTests.cs`; no new isolation
  infrastructure is required.

---

## Guards issues

This file specifies baseline tests that guard the following entries in
[`../../configuration-proposed-issues.md`](../../configuration-proposed-issues.md):

- **Issue 1** - Add `IValidateOptions<T>` and `ValidateOnStart` for all
  options classes. Guarded by: Sections 3.1, 3.2, 3.3, 3.4, 3.5, 3.6
  (default values, whitespace edge cases, chain-validation effect, CA-cert
  load failures, and deferred-throw characterisation that Issue 1 is
  expected to replace with early validation).

Reciprocal "Baseline tests required" lines should be added to Issue 1
citing this file. Those edits happen in the final cross-reference sweep,
not here.
