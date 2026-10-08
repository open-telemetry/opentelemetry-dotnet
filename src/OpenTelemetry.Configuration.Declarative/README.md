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

`OTEL_CONFIG_FILE` is read from the process environment, not from
`IConfiguration`. Setting it in `launchSettings.json`, a container, or a shell
works; setting it in `appsettings.json` or on the command line does not. To use
another source, pass the path to `UseDeclarativeConfiguration` instead.

A relative path is resolved against `AppContext.BaseDirectory` (the build or
publish output directory), not the content root that `appsettings.json` uses.
Copy the file to the output directory, or use an absolute path:

```xml
<ItemGroup>
  <None Update="otel-config.yaml" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

### 2. Wire it into your OTel setup

**On `IHostApplicationBuilder` (`WebApplicationBuilder` /**
**`HostApplicationBuilder`) - recommended:**

```csharp
builder.AddOpenTelemetry()
    .UseDeclarativeConfiguration()
    .WithTracing(b => b.AddSource("MyApp.*").AddConsoleExporter());
```

This approach adds the YAML source directly to `builder.Configuration`, so the
document can be read during registration, and it registers the host's resource
defaults (such as `service.name` from the application name).
`builder.Services.AddOpenTelemetry()` also works, but the source is only added
when `IConfiguration` is first resolved, and the host's resource defaults are
not registered.

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
of two. See [Strict mode](#strict-mode).

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

| YAML field | Effect | Application path |
| --- | --- | --- |
| `disabled` | Disables the OpenTelemetry SDK when `true` | Flat SDK key |
| `resource.attributes` | Adds typed structured resource attributes to all signals | Typed resource detector |
| `resource.attributes_list` | Adds resource attributes from a pre-formatted `key=value` list | Flat SDK key |
| `resource.schema_url` | Contributes a schema URL to the resource (see below) | Typed resource detector |

The following attribute types defined by the OTel configuration schema are
supported for `resource.attributes`:

- `string`
- `bool`
- `int` (stored as `long`)
- `double`
- `string_array` (stored as `string[]`)
- `bool_array` (stored as `bool[]`)
- `int_array` (stored as `long[]`)
- `double_array` (stored as `double[]`)

`resource.attributes_list` is treated as containing a `OTEL_RESOURCE_ATTRIBUTES`
string that has not been percent-encoded and is passed through without
modification. In particular, literal `+` in a value must be written as `%2B`,
otherwise the SDK will decode it as a space character.

`resource.schema_url` is merged with the schema URLs contributed by other
resource detectors, including the SDK's default resource, using the standard
[resource merge rules](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md#merge).
If the configured value differs from a schema URL contributed by another
detector, the final resource has no schema URL.

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

## Strict mode

Declarative configuration uses the specification's strict mode. When a
configuration file is used, other `OTEL_*` settings are ignored unless the
file references them through
[environment-variable substitution](#environment-variable-substitution).
This prevents settings outside the file from being combined with it
unintentionally.

To continue using an environment variable, reference it from the YAML file:

```yaml
file_format: "1.1"

disabled: ${OTEL_SDK_DISABLED:-false}
```

`OTEL_DOTNET_*` settings and configuration applied directly in code are not
affected by strict mode.

To keep other common settings, reference them in the same way:

```yaml
file_format: "1.1"

resource:
  attributes:
    - name: service.name
      value: ${OTEL_SERVICE_NAME:-my-service}
  attributes_list: ${OTEL_RESOURCE_ATTRIBUTES}
```

The specification's
[migration configuration](https://github.com/open-telemetry/opentelemetry-configuration/blob/main/examples/otel-sdk-migration-config.yaml)
references every standard environment variable and is a useful starting point.

Points to be aware of:

- Tools that inject `OTEL_*` environment variables, such as .NET Aspire or
  Kubernetes operators, are affected in the same way. Reference the variables
  they set from the file.
- On the `builder.AddOpenTelemetry()` path, `OTEL_SERVICE_NAME` and
  `OTEL_RESOURCE_ATTRIBUTES` no longer replace the host's default `service.name`
  unless the file references them.
- Configuration sources added *after* declarative configuration still override
  it, as do `OTEL_*` settings from sources outside the configuration it was
  added to.

The `OpenTelemetry:Exporters:Console` configuration section, which the Console
exporter binds into its options, is masked the same way as `OTEL_*` settings,
including its environment-variable spelling
(`OpenTelemetry__Exporters__Console__Targets`). Only the paths the SDK binds are
masked. Other keys under `OpenTelemetry` stay visible.

## Known current limitations

> [!NOTE]
> These represent limitations of the current implementation. They may be
> resolved as this project develops and prior to release.

- Only the settings listed above are supported.
- File watching is not supported; the YAML file is read once at start-up.
  Calling `IConfigurationRoot.Reload()` does not re-read the YAML file or change
  the configuration in use. The reload is ignored and a warning is emitted via
  EventSource.
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
- Some components read environment variables directly and are not covered by
  strict mode: including options created with a public
  parameterless constructor in application code (for example
  `new OtlpExporterOptions()`), and a `ResourceBuilder` built without a service
  provider.
- When the source is registered with `AddOpenTelemetryDeclarativeConfiguration()`
  alone, strict mode applies but its warnings are not written.

### Pitfalls to avoid

- `UseDeclarativeConfiguration()` requires `IConfiguration` to already be
  registered when it runs. If the host registers `IConfiguration` later, the
  YAML source will not be visible to the SDK.
- A second call to `UseDeclarativeConfiguration()` on the same
  `IServiceCollection` is ignored. Only the first file path applies; a later
  call with a different path does not replace it.
- Configuration sources added after declarative configuration may override
  values from the file.

## Provide feedback

Please provide feedback on [issue #6380](https://github.com/open-telemetry/opentelemetry-dotnet/issues/6380)
if you are using or evaluating declarative configuration in your application.

Any feedback will help inform decisions about when to expose the API as stable
and what the final surface should look like.
