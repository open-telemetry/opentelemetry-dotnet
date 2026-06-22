# OpenTelemetry.Configuration.Declarative

> [!WARNING]
> This is an experimental package. APIs may change or be removed in
> future releases.

A partial experimental implementation of the [OpenTelemetry
declarative-configuration
specification](https://opentelemetry.io/docs/languages/sdk-configuration/declarative-configuration/)
for the OpenTelemetry .NET SDK.

Declarative configuration allows you to configure the OpenTelemetry SDK using a
YAML file instead of (or in addition to) environment variables and code-based
setup. This package implements a subset of the stable OTel declarative
configuration specification. It accepts any `file_format: "1.x"` document and
has been built against schema v1.1.

## Getting started

### 1. Set the config file path

```bash
OTEL_CONFIG_FILE=/path/to/otel-config.yaml
```

### 2. Wire it into your OTel setup

**Recommended on `HostApplicationBuilder` / `WebApplicationBuilder`:**

```csharp
builder.Configuration.AddOpenTelemetryDeclarativeConfiguration(); // reads OTEL_CONFIG_FILE
builder.Services.AddOpenTelemetry()
    .WithTracing(b => b.AddSource("MyApp.*").AddConsoleExporter());
```

**With `HostBuilder`**, add the source inside `ConfigureAppConfiguration`:

```csharp
hostBuilder.ConfigureAppConfiguration(b =>
    b.AddOpenTelemetryDeclarativeConfiguration("otel-config.yaml"));
hostBuilder.ConfigureServices(services =>
    services.AddOpenTelemetry().WithTracing(...));
```

**Alternative:** wire through `IOpenTelemetryBuilder` (reads `OTEL_CONFIG_FILE`
when called without a path):

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

`UseDeclarativeConfiguration()` works best on modern hosts
(`WebApplicationBuilder`, `HostApplicationBuilder`) where `IConfiguration` is
already registered before `AddOpenTelemetry()` is called. With `HostBuilder`,
use the `ConfigureAppConfiguration` approach instead so the YAML source is added
before DI configuration is built. Calling `UseDeclarativeConfiguration()` twice
on the same `IServiceCollection` is a no-op - the first file path wins and a
warning is emitted via EventSource. Calling it with a different path does not
replace the first registration.

### 3. Write a YAML config file

```yaml
file_format: "1.1"

resource:
  attributes:
    - name: service.name
      value: ${SERVICE_NAME:-my-service}
    - name: service.version
      value: "1.0.0"
```

## Supported settings

| YAML field | SDK effect |
| --- | --- |
| `disabled: true` | `OTEL_SDK_DISABLED` - builds a no-op provider |
| `resource.attributes` | `OTEL_RESOURCE_ATTRIBUTES` - resource attributes on all signals |
| `resource.attributes_list` | `OTEL_RESOURCE_ATTRIBUTES` - resource attributes in pre-formatted `key=value` list form |

`resource.attributes_list` is treated as containing a `OTEL_RESOURCE_ATTRIBUTES`
string that has not been percent-encoded and is passed through without
modification. In particular, literal `+` in a value must be written as `%2B`,
otherwise the SDK will decode it as a space character. Use `resource.attributes`
when you need the encoding to be handled automatically.

All other top-level sections (e.g. `tracer_provider`, `propagator`) are logged
and ignored. You can track this issue for missing features:
[#6380](https://github.com/open-telemetry/opentelemetry-dotnet/issues/6380).

## Environment-variable substitution

Values in the YAML file may reference environment variables using the `${...}`
syntax, per the OTel spec:

| Syntax | Meaning |
| --- | --- |
| `${MY_VAR}` | Value of `MY_VAR` environment variable |
| `${env:MY_VAR}` | Same with explicit `env:` prefix |
| `${MY_VAR:-default}` | Value of `MY_VAR`, or `default` if undefined/empty |
| `$$` | Literal `$` (escape) - so `$${MY_VAR}` yields literal `${MY_VAR}` |

Undefined variables without a default resolve to an empty string.

## Precedence

When you call `UseDeclarativeConfiguration()` or
`AddOpenTelemetryDeclarativeConfiguration()`, the YAML source is **appended
after** all sources already registered on the builder at that point. That means
declarative configuration **takes precedence over** environment variables,
`appsettings.json`, and other sources that were registered earlier.

Sources you add **after** that call take precedence over YAML values (same as
standard `IConfiguration` ordering).

> [!NOTE]
> `OTEL_SDK_DISABLED` and `OTEL_RESOURCE_ATTRIBUTES` are read directly
> from `IConfiguration`, not via `IOptions<T>`, so `Configure<T>()` /
> `PostConfigure<T>()` cannot override them. Use a higher-priority
> `IConfiguration` source (e.g. `AddInMemoryCollection`) instead.

## Known limitations

- File watching is not supported; the YAML file is read once at start-up.
- `resource.attributes` values are percent-encoded per the OTel specification.
  For duplicate attribute names the first occurrence wins.
- Unknown YAML sections are logged and ignored rather than causing an error.
- Plain (unquoted) YAML scalars that resolve to `null`, `Null`, `NULL`, or `~`
  after environment variable substitution are treated as YAML null and the
  setting is silently ignored. To preserve the string `"null"` as a value, use a
  quoted scalar: `value: "null"`. This is consistent with YAML 1.2 Core Schema
  semantics applied post-substitution as required by the OTel specification.

## Further reading

Design and implementation details (`IOptions` vs `IConfiguration`, substitution
ordering, null semantics, runtime disable behavior) are documented in
[OTEL1006](../../docs/diagnostics/experimental-apis/OTEL1006.md#implementation-notes).
