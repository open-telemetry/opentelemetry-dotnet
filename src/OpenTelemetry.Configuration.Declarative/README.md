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

**On `IHostApplicationBuilder` (`WebApplicationBuilder` /**
**`HostApplicationBuilder`) - recommended:**

```csharp
builder.AddOpenTelemetry()
    .UseDeclarativeConfiguration()
    .WithTracing(b => b.AddSource("MyApp.*").AddConsoleExporter());
```

**With `HostBuilder`**, add the source inside `ConfigureAppConfiguration`:

```csharp
hostBuilder.ConfigureAppConfiguration(b =>
    b.AddOpenTelemetryDeclarativeConfiguration("otel-config.yaml"));
hostBuilder.ConfigureServices(services =>
    services.AddOpenTelemetry()
        .UseDeclarativeConfiguration("otel-config.yaml")
        .WithTracing(...));
```

**Without a host** (plain `IServiceCollection`), wire through
`IOpenTelemetryBuilder` (reads `OTEL_CONFIG_FILE` when called without a path):

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

Calling `UseDeclarativeConfiguration()` twice on the same `IServiceCollection`
is a no-op and the first file path wins.

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

## Read the parsed document

Applications and distributions can read the complete parsed document, including
sections that this package retains but does not yet apply:

```csharp
var document =
    serviceProvider.GetOpenTelemetryDeclarativeConfiguration();

if (document is not null)
{
    if (document.Properties
        .GetMapping("distribution")
        .TryGetValue(out var distribution))
    {
        var name = distribution.GetString("name");
    }
}
```

Use the `IServiceProvider` overload after the application has been built. When
no service provider is available, such as during registration, read the
document from configuration instead:

```csharp
builder.Configuration
    .AddOpenTelemetryDeclarativeConfiguration("otel-config.yaml");

var document =
    builder.Configuration.GetOpenTelemetryDeclarativeConfiguration();
```

If more than one declarative configuration file is found, the file with the
highest priority is used and a warning is logged. Register only one file per
application.

## Supported settings

| YAML field | Effect | Application path | Later `IConfiguration` source overrides |
| --- | --- | --- | --- |
| `disabled` | Disables the OpenTelemetry SDK when `true` | Flat SDK key | Yes |
| `resource.attributes` | Adds typed structured resource attributes to all signals | Typed resource detector | No |
| `resource.attributes_list` | Adds resource attributes from a pre-formatted `key=value` list | Flat SDK key | Yes |
| `resource.schema_url` | Sets the schema URL on the resource | Typed resource detector | No |

### Configuration layering boundary

The original "YAML is another .NET configuration source" model applies only to
simple settings that map cleanly onto existing SDK configuration keys.
Structured declarative configuration is model-driven, so later configuration
sources cannot implicitly override it.

All eight attribute types defined by the OTel configuration schema are
supported: `string`, `bool`, `int`, `double`, `string_array`, `bool_array`,
`int_array`, `double_array`. Each type reaches the built `Resource` with its
declared CLR type (`int` maps to `long`, `double` remains `double`, and array
types map to their corresponding one-dimensional CLR array type).

`resource.attributes_list` is treated as containing a `OTEL_RESOURCE_ATTRIBUTES`
string that has not been percent-encoded and is passed through without
modification. In particular, literal `+` in a value must be written as `%2B`,
otherwise the SDK will decode it as a space character.

`resource.attributes` and `resource.schema_url` are only applied when using
`UseDeclarativeConfiguration()` on an `IOpenTelemetryBuilder`. When the source
is registered via `AddOpenTelemetryDeclarativeConfiguration()` alone (the
source-only path), `resource.attributes` entries and `resource.schema_url` are
not applied to the SDK resource; only `resource.attributes_list` and `disabled`
take effect on that path.

All other top-level sections (e.g. `tracer_provider`, `propagator`) are logged
and are not applied. Structurally invalid content can fail configuration
loading.

## Parsing and validation

The configuration file is expected to conform to the YAML 1.2 specification.

YAML 1.1 merge keys (`<<: *defaults`) are rejected before any configuration
is interpreted. Quoted or explicitly string-tagged `<<` keys remain ordinary
property names under the YAML 1.2 core schema.

### Environment-variable substitution

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
flat settings projected by the declarative source **take precedence over**
environment variables, `appsettings.json`, and other sources that were
registered earlier.

For those flat settings, sources added **after** the declarative source take
precedence using standard `IConfiguration` ordering. The currently supported
flat settings are `disabled` and `resource.attributes_list`.

This source ordering does not apply to model-driven settings such as
`resource.attributes` and `resource.schema_url`, or between two declarative
configuration files. Flat keys can be merged per key; the typed YAML document
cannot, so exactly one document is used.

## Known current limitations

> [!NOTE]
> These represent limitations of the current implementation. They may be
> resolved as this project develops and prior to release.

- Only the settings listed above are supported.
- File watching is not supported; the YAML file is read once at start-up.
  Calling `IConfigurationRoot.Reload()` does not re-read the YAML file or change
  the configuration in use. The reload is ignored and a warning is emitted via
  EventSource.
- The package uses standard `IConfiguration` source ordering for flat keys. It
  does not yet provide the specification's strict mode that ignores other SDK
  environment variables when `OTEL_CONFIG_FILE` is set.
- `UseDeclarativeConfiguration()` applies YAML values by extending the
  configuration available to it at the time it is called. An application that
  replaces its `IConfiguration` registration, or clears its configuration
  sources, *after* that call detaches the YAML source: flat keys lose the YAML
  values while typed consumers still read the document. Following
  `builder.AddOpenTelemetry()` the source is added to `builder.Configuration`
  directly, so a later `builder.Configuration.Sources.Clear()` detaches it;
  following `services.AddOpenTelemetry()` (non-host) the source is added to the
  `IConfiguration` resolved from the container, so a later registration of
  `IConfiguration` detaches it. Register declarative configuration after your
  configuration sources are settled.
- For duplicate structured resource attribute names, the first occurrence wins.
- Unknown top-level sections are logged and are not applied, but their `${...}`
  references are resolved during the load, so an unset variable in one is
  reported.
- `resource.detection/development` (the SDK's resource detector discovery
  mechanism) is not yet implemented.

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
