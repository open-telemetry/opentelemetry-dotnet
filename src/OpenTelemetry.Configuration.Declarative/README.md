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
has been built against the OpenTelemetry configuration schema v1.2.

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
    .UseDeclarativeConfiguration();
```

With a valid `tracer_provider` section, this call enables tracing and adds the
processors and exporters from the file. No separate `WithTracing()` call is
needed. See [Tracing from the document](#tracing-from-the-document).

This approach adds the YAML source directly to `builder.Configuration`, so the
document can be read during registration, and it registers the host's resource
defaults (such as `service.name` from the application name).

> [!NOTE]
> `builder.Services.AddOpenTelemetry()` also works, but the source is only added
> when `IConfiguration` is first resolved, and the host's resource defaults are
> not registered.

The file is read during `UseDeclarativeConfiguration()` on all registration
paths. Some errors fail this call; others fail when the tracer provider is
built. See [What fails at start-up](#what-fails-at-start-up).

**With `HostBuilder`**, register it inside `ConfigureServices`. The source is
added to the host's configuration when `IConfiguration` is first resolved:

```csharp
hostBuilder.ConfigureServices(services =>
    services.AddOpenTelemetry()
        .UseDeclarativeConfiguration("otel-config.yaml"));
```

**Without a host** (plain `IServiceCollection`), wire through
`IOpenTelemetryBuilder` (reads `OTEL_CONFIG_FILE` when called without a path):

```csharp
services.AddOpenTelemetry()
    .UseDeclarativeConfiguration();
```

Or, to load from an explicit path (ignoring `OTEL_CONFIG_FILE`):

```csharp
services.AddOpenTelemetry()
    .UseDeclarativeConfiguration("otel-config.yaml");
```

Calling `UseDeclarativeConfiguration()` twice on the same `IServiceCollection`
is a no-op and the first file path wins.

> [!IMPORTANT]
> Only one declarative configuration file is supported per
> `IConfigurationBuilder`. Registering the same file again is a no-op.
> Registering a different file leaves the first one in effect. Declarative
> configuration files are not layered against each other: one YAML document is
> chosen, never a merge of two. See [Strict mode](#strict-mode).

### 3. Write a YAML config file

```yaml
file_format: "1.1"

resource:
  attributes:
    - name: service.name
      value: ${SERVICE_NAME:-my-service}
    - name: service.version
      value: "1.0.0"

tracer_provider:
  processors:
    - batch:
        exporter:
          console:
```

The [`DeclarativeConfiguration` example](../../examples/DeclarativeConfiguration/README.md)
runs this end to end.

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

Use the `IServiceProvider` overload after the application has been built. To
read the document during registration on `IHostApplicationBuilder`, call
`UseDeclarativeConfiguration` first, then read it from `builder.Configuration`:

```csharp
builder.AddOpenTelemetry()
    .UseDeclarativeConfiguration("otel-config.yaml");

var document =
    builder.Configuration.GetOpenTelemetryDeclarativeConfiguration();
```

With `HostBuilder` or a plain `IServiceCollection`, the document is available
once the service provider has been built.

If more than one declarative configuration file is found, the file with the
highest priority is used and a warning is logged. Register only one file per
application.

## Supported settings

| YAML field | Effect |
| --- | --- |
| `disabled` | Disables the OpenTelemetry SDK when `true` |
| `log_level` | Sets the minimum level of the SDK's self-diagnostics (see [`log_level`](#log_level)) |
| `resource.attributes` | Adds typed resource attributes to all signals |
| `resource.attributes_list` | Adds resource attributes from a `key=value` list |
| `resource.schema_url` | Sets a resource schema URL, subject to merge rules below |
| `tracer_provider` | Enables tracing |
| `tracer_provider.processors` | Adds `simple` and `batch` span processors with the `console` exporter |
| `tracer_provider.sampler` | Sets the sampler: `always_on`, `always_off`, `always_record`, `trace_id_ratio_based` or `parent_based` |
| `tracer_provider.tracer_configurator/development` | Selects which `ActivitySource` names to listen to |
| `tracer_provider.id_generator` | Supports `random`, the SDK's default |

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
modification.

> [!WARNING]
> A literal `+` in an `attributes_list` value must be written as `%2B`,
> otherwise the SDK will decode it as a space character.

`resource.schema_url` is merged with the schema URLs contributed by other
resource detectors, including the SDK's default resource, using the standard
[resource merge rules](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md#merge).
If the configured value differs from a schema URL contributed by another
detector, the final resource has no schema URL.

Other schema-defined SDK settings (e.g. `meter_provider`, `propagator`) are
retained but not applied. Outside `tracer_provider`, unsupported stable settings
are logged as errors when the document is loaded. Unsupported experimental
settings are logged as warnings. These messages do not stop loading.

For unsupported stable settings in `tracer_provider`, this package throws when
building the tracer provider rather than ignoring the settings. See
[What fails at start-up](#what-fails-at-start-up).

Only the outermost unsupported property is reported, without its nested
properties. Non-empty lists are reported per item.

### `log_level`

`log_level` sets the minimum level of the SDK's self-diagnostics. It does not
enable output; configure an output separately, for example with
`OTEL_DOTNET_SELF_DIAGNOSTICS_SINKS`.

Accepted values are lowercase `trace`, `debug`, `info`, `warn`, `error` and
`fatal`, optionally suffixed with `2`, `3` or `4`. Suffixed values map to the
same .NET log level as their base value; `fatal` maps to `Critical`. Other
values, including `none`, are rejected.

Other schema-defined SDK settings (e.g. `tracer_provider`, `propagator`) are
retained but not applied. Unsupported stable settings are logged as errors and
experimental settings as warnings when the document is loaded. Only the
outermost unsupported property is reported, without its nested properties.
Non-empty sequences are reported per item. Loading still succeeds, although
structurally invalid content can fail configuration loading.

`instrumentation/development` and `distribution` are retained for consumers
through `DeclarativeConfigurationDocument.Properties`. The SDK does not
interpret these sections, and they do not generate unsupported-setting
diagnostics.

## Tracing from the document

A valid `tracer_provider` section enables tracing. You can also call
`WithTracing(...)` in code, before or after `UseDeclarativeConfiguration()`:
both configure the same tracer provider.

### What is applied

- **Sources.** Every `ActivitySource` is listened to by default. On .NET 10 and
  later this includes the runtime's own sources, such as HTTP client spans and
  the experimental `Experimental.System.Net.*` connection sources. To listen
  only to selected sources, set `default_config.enabled` to `false` and enable
  the sources you want:

  ```yaml
  tracer_provider:
    processors:
      - batch:
          exporter:
            console:
    tracer_configurator/development:
      default_config:
        enabled: false
      tracers:
        - name: MyApp.*
          config:
            enabled: true
  ```

  `*` matches any number of characters and `?` matches one. Names are matched
  ignoring case. See [Source-selection limitations](#source-selection-limitations)
  before using overlapping patterns or `enabled: false` entries.
- **Processors.** `simple` and `batch` span processors, in document order. The
  four batch settings (`schedule_delay`, `export_timeout`, `max_queue_size`,
  `max_export_batch_size`) use the specification's defaults when omitted or
  null. Delays and timeouts are in milliseconds.
  `OTEL_BSP_*` environment variables are not read. `schedule_delay: 0` is
  replaced by 1 millisecond with a warning, because the SDK requires a delay.
- **Exporter.** `console`, with default options. Code and `IConfiguration`
  cannot change the options of this exporter.
- **Sampler.** `always_on`, `always_off`, `always_record`,
  `trace_id_ratio_based` and `parent_based`, nested to any depth:

  ```yaml
  tracer_provider:
    processors:
      - simple:
          exporter:
            console:
    sampler:
      parent_based:
        root:
          trace_id_ratio_based:
            ratio: 0.25
  ```

  An omitted or null `ratio` is 1. An omitted `parent_based` child uses the
  specification's default: `always_on` for `root`, `remote_parent_sampled` and
  `local_parent_sampled`, and `always_off` for the two `not_sampled` children.
  When `sampler` is omitted, the SDK default applies, a `parent_based` sampler
  with an `always_on` root.

### Adding tracing settings in code

Code still adds to the pipeline:

- `AddSource` adds to the sources selected by the file. It can enable a source
  the file disables.
- Processors from code and from the document both run, in the order of the
  calls. A processor that changes spans when they end should be added before
  `UseDeclarativeConfiguration()`. Adding a console exporter in both code and
  YAML prints each span twice.

### Precedence

Code wins over the document for the sampler, wherever it is called. A
`SetSampler` call replaces the document's sampler whether it comes before or
after `UseDeclarativeConfiguration()`.

### What fails at start-up

A `DeclarativeConfigurationException` naming the setting, its line and its
column is thrown for:

- Invalid `tracer_provider` settings, such as a missing or empty `processors`
  list or invalid processor, exporter or source-selection entries. These fail
  in `UseDeclarativeConfiguration()`.
- A `sampler` entry that does not name exactly one sampler. This fails in
  `UseDeclarativeConfiguration()`.
- Invalid settings inside a sampler, such as a `ratio` value outside of the range
  0 to 1 inclusive, an `always_record` sampler without a `root`, or a
  `parent_based` child that does not name exactly one sampler. These fail when
  the tracer provider is built.
- Valid `tracer_provider` settings that are not supported by declarative
  configuration. These fail when the tracer provider is built, in one exception
  that lists them all. A sampler nested in `parent_based` or `always_record`
  that is not supported fails on its own, at the point the sampler is created.
  See [Supported settings](#supported-settings).

### What is logged

Unsupported settings outside `tracer_provider` are logged as not applied, as
described under [Supported settings](#supported-settings).

### Source-selection limitations

The package selects sources using `AddSource`, which can add sources but cannot
exclude them from a broader pattern:

> [!WARNING]
> With the default enabled, `enabled: false` entries cannot disable sources.

- An entry is ignored if an earlier entry has the same name (ignoring case).
  Other overlapping patterns may still enable a source marked
  `enabled: false`.
- A warning is logged if the file may enable a source it also disables. Some
  warnings may be unnecessary. Sources added in code are not checked.
- A source that is not listened to creates no span: `StartActivity` returns
  `null`. The specification describes a disabled tracer as producing
  non-recording spans instead.

> [!TIP]
> For predictable results, set `default_config.enabled: false` and use only
> `enabled: true` entries for the sources you want.

### Multiple configuration files

Use one declarative configuration file per application. If shared configuration
already contains another file, it may supply the processors while the file
passed to `UseDeclarativeConfiguration()` determines whether tracing is enabled
and which sources are selected. A warning is logged when the files differ.

## Parsing and validation

The configuration file is expected to conform to the YAML 1.2 specification.

Mappings and sequences may be nested up to depth 128, with the document root at
depth zero. This applies to the whole document, including custom sections and
paths through YAML aliases.

An alias used as a mapping key must refer to an anchor already defined in the
same document. Invalid alias keys are rejected during parsing.

YAML 1.1 merge keys (`<<: *defaults`) are rejected before any configuration
is interpreted. Quoted or explicitly string-tagged `<<` keys remain ordinary
property names under the YAML 1.2 core schema.

### Property names

Property names are checked against the OpenTelemetry configuration schema. A
property the schema does not define is an error wherever the schema does not
allow additional properties.

Additional properties are retained and readable through
`DeclarativeConfigurationDocument.Properties` where the schema allows them.

Undefined properties are retained with a warning, instead of being an error, in
two cases:

- The document declares a `file_format` minor version newer than the schema
  version this package was built against, which may define properties the
  package does not know.
- The property is, or is nested in, a property whose name ends in
  `/development`, `/alpha` or `/beta`. These are exempt from the schema's
  stability guarantees.

In both cases a misspelled property name is reported as a warning and is not
applied.

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

## Known current limitations

> [!NOTE]
> These represent limitations of the current implementation. They may be
> resolved as this project develops and prior to release.

- Only the settings listed above are supported. Signals other than tracing are
  still configured in code.
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
- `${...}` references are resolved throughout the document, including settings
  the SDK does not interpret. An unset variable without a default is reported
  at Verbose level.
- `resource.detection/development` (the SDK's resource detector discovery
  mechanism) is accepted but not yet implemented.
- Full schema validation is not implemented. Tracing setup checks component
  entries, required processor/exporter settings, batch values and
  source-selection settings; not every value in the document is validated.
- Property names are validated against the schema, but value types, ranges and
  required properties are not, apart from `log_level`.
- Some components read environment variables directly and are not covered by
  strict mode: including options created with a public
  parameterless constructor in application code (for example
  `new OtlpExporterOptions()`), and a `ResourceBuilder` built without a service
  provider.
- Declarative configuration is registered through `IOpenTelemetryBuilder`.
  Providers built directly with `Sdk.CreateTracerProviderBuilder()`, or the
  meter and logger equivalents, cannot use it yet, and there is no API to parse
  a file without registering it.

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
