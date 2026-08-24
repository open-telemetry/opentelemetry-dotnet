# OpenTelemetry.Configuration.Declarative

> [!WARNING]
> This is an experimental package. APIs may change or be removed in
> future releases.

A partial experimental implementation of the
[OpenTelemetry declarative-configuration specification](https://opentelemetry.io/docs/languages/sdk-configuration/declarative-configuration/)
for the OpenTelemetry .NET SDK.

Declarative configuration allows you to configure the OpenTelemetry SDK using a
YAML file instead of (or in addition to) environment variables and code-based
setup. This package implements a subset of the stable OTel declarative
configuration specification. It accepts any `file_format: "1.x"` document and
has been built against the OpenTelemetry configuration schema v1.1.

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

Only one declarative configuration file is supported per
`IConfigurationBuilder`. Registering the same file again is a no-op. Registering
a different file leaves the first one in effect. Declarative configuration files
are not layered against each other: one YAML document is chosen, never a merge
of two. See [Precedence](#precedence).

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

| YAML field | Effect |
| --- | --- |
| `disabled` | Disables the OpenTelemetry SDK when `true` |
| `resource.attributes` | Adds structured resource attributes to all signals |
| `resource.attributes_list` | Adds resource attributes from a pre-formatted `key=value` list |

`resource.attributes_list` is treated as containing a `OTEL_RESOURCE_ATTRIBUTES`
string that has not been percent-encoded and is passed through without
modification. In particular, literal `+` in a value must be written as `%2B`,
otherwise the SDK will decode it as a space character. Use `resource.attributes`
when you need the encoding to be handled automatically.

Only string-valued `resource.attributes` are currently supported. Boolean,
integer, double, and array attributes are reported and skipped.

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

YAML escape sequences are decoded before environment-variable substitution.
Consequently, defaults cannot contain characters, such as newlines, that the
OpenTelemetry substitution grammar excludes.

Quoting remains significant after substitution:

```yaml
file_format: "1.1"       # string - accepted
file_format: 1.1         # number - rejected
disabled: true           # boolean - accepted
disabled: "true"         # string - rejected
value: ${PORT}           # may resolve to a number
value: "${PORT}"         # always resolves to a string
```

## Precedence

When you call `UseDeclarativeConfiguration()` or
`AddOpenTelemetryDeclarativeConfiguration()`, the YAML source is **appended
after** all sources already registered on the builder at that point. That means
declarative configuration **takes precedence over** environment variables,
`appsettings.json`, and other sources that were registered earlier.

Sources you add **after** that call take precedence over YAML values (same as
standard `IConfiguration` ordering).

This layering applies between YAML and other kinds of configuration source
(environment variables, `appsettings.json`, in-memory values, etc.). It
does not apply between two declarative configuration files. Flat keys can be
merged per key; the typed YAML document cannot, so exactly one document is used.

## Known limitations

- Only the settings listed above are supported.
- File watching is not supported; the YAML file is read once at start-up.
  Calling `IConfigurationRoot.Reload()` does not re-read the YAML file or change
  the configuration in use. The reload is ignored and a warning is emitted via
  EventSource.
- The package uses standard `IConfiguration` source ordering. It does not yet
  provide the specification's strict mode that ignores other SDK environment
  variables when `OTEL_CONFIG_FILE` is set.
- `UseDeclarativeConfiguration()` applies YAML values by extending the
  `IConfiguration` registered at the time it is called. An application that
  replaces its `IConfiguration` registration, or clears its configuration
  sources, *after* that call detaches the YAML source: flat keys lose the YAML
  values while typed consumers still read the document. Register declarative
  configuration after your configuration sources are settled, or use
  `builder.Configuration.AddOpenTelemetryDeclarativeConfiguration()`, which adds
  the source directly and is not affected.
- Only string-valued structured resource attributes are emitted. Unsupported
  typed attributes are skipped and reported.
- For duplicate structured resource attribute names, the first occurrence wins.
  A structured attribute also takes precedence over the same name in
  `resource.attributes_list`, even when its type is not currently supported.
- Unknown top-level sections are logged and ignored. Unknown fields within
  `resource` or a resource attribute are schema errors and fail the load.
- YAML merge keys (`<<: *defaults`) are rejected. Merge keys are a YAML 1.1
  feature and are not part of the required YAML 1.2 core schema.
- Plain (unquoted) YAML scalars that resolve to `null`, `Null`, `NULL`, or `~`
  after environment variable substitution are treated as YAML null. Nullable
  fields apply their specified null behaviour; non-nullable fields fail schema
  validation. To preserve the string `"null"` as a value, use a quoted scalar:
  `value: "null"`. This is consistent with YAML 1.2 core schema semantics
  applied post-substitution as required by the OTel specification.

### Pitfalls to avoid

- `UseDeclarativeConfiguration()` requires `IConfiguration` to already be
  registered when it runs. If the host registers `IConfiguration` later, the
  YAML source will not be visible to the SDK.
- A second call to `UseDeclarativeConfiguration()` on the same
  `IServiceCollection` is ignored. Only the first file path applies; a later
  call with a different path does not replace it.

## Provide feedback

Please provide feedback on [issue #6380](https://github.com/open-telemetry/opentelemetry-dotnet/issues/6380)
if you are using or evaluating declarative configuration in your application.

Any feedback will help inform decisions about when to expose the API as stable
and what the final surface should look like.
