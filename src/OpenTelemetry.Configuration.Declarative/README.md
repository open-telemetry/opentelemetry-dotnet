# OpenTelemetry.Configuration.Declarative

> [!WARNING]
> This is an experimental package. APIs may change or be removed in future releases.

A partial experimental implementation of the
[OpenTelemetry declarative-configuration specification](https://opentelemetry.io/docs/languages/sdk-configuration/declarative-configuration/)
for the OpenTelemetry .NET SDK.

Declarative configuration allows you to configure the OpenTelemetry SDK using a
YAML file instead of (or in addition to) environment variables and code-based
setup. This package implements a subset of the stable OTel spec v1.0
(`file_format: "1.0"`).

## Getting started

### 1. Set the config file path

```
OTEL_CONFIG_FILE=/path/to/otel-config.yaml
```

### 2. Wire it into your OTel setup

**Recommended on `HostApplicationBuilder` / `WebApplicationBuilder`:**

```csharp
builder.Configuration.AddOpenTelemetryDeclarativeConfiguration(); // reads OTEL_CONFIG_FILE
builder.Services.AddOpenTelemetry()
    .WithTracing(b => b.AddSource("MyApp.*").AddConsoleExporter());
```

**On classic `HostBuilder`**, add the source inside `ConfigureAppConfiguration`:

```csharp
hostBuilder.ConfigureAppConfiguration(b =>
    b.AddOpenTelemetryDeclarativeConfiguration("otel-config.yaml"));
hostBuilder.ConfigureServices(services =>
    services.AddOpenTelemetry().WithTracing(...));
```

**Alternative:** wire through `IOpenTelemetryBuilder` (reads `OTEL_CONFIG_FILE` when called
without a path):

```csharp
services.AddOpenTelemetry()
    .UseDeclarativeConfiguration()
    .WithTracing(b => b.AddSource("MyApp.*").AddConsoleExporter());
```

Or, to load from an explicit path (ignoring `OTEL_CONFIG_FILE`):

```csharp
services.AddOpenTelemetry()
    .UseDeclarativeConfiguration("otel-config.yaml")
    .WithTracing(...);
```

> [!IMPORTANT]
> **Integration pitfalls**
>
> - **`UseDeclarativeConfiguration()` requires `IConfiguration` to exist** when it runs.
>   If host infrastructure registers `IConfiguration` *after* your OTel setup, the YAML
>   source will be unreachable. Prefer `builder.Configuration.AddOpenTelemetryDeclarativeConfiguration()`
>   on modern hosts, or `ConfigureAppConfiguration` on classic `HostBuilder`.
> - **Calling `UseDeclarativeConfiguration()` twice** on the same `IServiceCollection`
>   is a no-op after the first call. Only the **first** file path applies; a second call
>   with a different path is ignored (an EventSource warning is emitted).

### 3. Write a YAML config file

```yaml
file_format: "1.0"

resource:
  attributes:
    - name: service.name
      value: ${SERVICE_NAME:-my-service}
    - name: service.version
      value: "1.0.0"
```

## Supported settings (this release)

| YAML field | SDK effect |
|---|---|
| `disabled: true` | `OTEL_SDK_DISABLED` - builds a no-op provider |
| `resource.attributes` | `OTEL_RESOURCE_ATTRIBUTES` - resource attributes on all signals |
| `resource.attributes_list` | `OTEL_RESOURCE_ATTRIBUTES` - resource attributes in pre-formatted `key=value` list form |

All other top-level sections (e.g. `tracer_provider`, `propagator`) are logged
and ignored. Full component support will require the factory contract to be
implemented (see [#6380](https://github.com/open-telemetry/opentelemetry-dotnet/issues/6380)).

## Environment-variable substitution

Values in the YAML file may reference environment variables using the
`${...}` syntax, per the OTel spec:

| Syntax | Meaning |
|---|---|
| `${MY_VAR}` | Value of `MY_VAR` environment variable |
| `${env:MY_VAR}` | Same with explicit `env:` prefix |
| `${MY_VAR:-default}` | Value of `MY_VAR`, or `default` if undefined/empty |
| `$$` | Literal `$` (escape) - so `$${MY_VAR}` yields literal `${MY_VAR}` |

Undefined variables without a default resolve to empty string.

## Precedence

When you call `UseDeclarativeConfiguration()` or
`AddOpenTelemetryDeclarativeConfiguration()`, the YAML source is **appended
after** all sources already registered on the builder at that point. That means
declarative configuration **takes precedence over** environment variables,
`appsettings.json`, and other sources that were registered earlier.

Sources you add **after** that call take precedence over YAML values (same as
standard `IConfiguration` ordering).

> [!NOTE]
> `services.Configure<T>()` / `PostConfigure<T>()` delegates run through the
> .NET Options pipeline and can override values from `IConfiguration` sources,
> but only when the SDK reads the relevant setting via `IOptions<T>`. The two
> settings supported in this release (`OTEL_SDK_DISABLED`,
> `OTEL_RESOURCE_ATTRIBUTES`) are read by the SDK directly from
> `IConfiguration`, so they are not affected by the Options pipeline. To
> override them in code, add a higher-priority `IConfiguration` source after
> the YAML source (e.g. `AddInMemoryCollection`) rather than using
> `Configure<T>`.

A future **strict mode** will restrict the SDK to YAML-only OTel configuration,
ignoring host environment variables and `appsettings.json` for OTel keys (see
[#6380](https://github.com/open-telemetry/opentelemetry-dotnet/issues/6380)).

## Known limitations

- No file watching: the YAML file is read once at startup. Runtime reload is a
  planned future workstream.
- `resource.attributes` values are percent-encoded per the OTel spec (`,`, `=`,
  `%`, and `+` are encoded; other characters pass through). Attribute **keys** are
  not encoded; keep them to `[a-z0-9._-]` per OTel semantic conventions. Duplicate
  attribute names: first occurrence wins; subsequent duplicates are logged and
  skipped. `resource.attributes_list` values are passed through as-is (they are
  already in `OTEL_RESOURCE_ATTRIBUTES` format). When both fields are present,
  `attributes` entries take higher priority. `attributes_list` is comma-split
  using the same naive delimiter rules as `OtelEnvResourceDetector`: commas inside
  values must be percent-encoded as `%2C`; unencoded commas are treated as entry
  separators and will corrupt parsing.
- Schema validation is lenient: only sections this package handles are validated;
  unknown sections are logged and ignored rather than causing an error. Full JSON
  schema validation is planned for complete Parse compliance.

## Further reading

Design and implementation details (IOptions vs `IConfiguration`, substitution
ordering, null semantics, runtime disable behavior) are documented in
[OTEL1006](../../docs/diagnostics/experimental-apis/OTEL1006.md#implementation-notes).
