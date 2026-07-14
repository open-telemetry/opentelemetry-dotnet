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

### *Pitfalls to avoid:

* `UseDeclarativeConfiguration()` requires `IConfiguration` to already be
  registered** when it runs. If the host registers `IConfiguration` later, the
  YAML source will not be visible to the SDK.
* A second call to `UseDeclarativeConfiguration()` on the same
  `IServiceCollection` is ignored.** Only the first file path applies; a later
  call with a different path does not replace it (an EventSource warning is
  emitted).

## Implementation notes

### `IOptions` vs direct `IConfiguration` reads

The two settings currently supported (`OTEL_SDK_DISABLED`,
`OTEL_RESOURCE_ATTRIBUTES`) are consumed by the SDK via direct `IConfiguration`
reads rather than the .NET `IOptions<T>` pipeline:

* `OTEL_SDK_DISABLED` is read before the provider is constructed to decide
  whether to return a real provider or a no-op.
* `OTEL_RESOURCE_ATTRIBUTES` is read by the resource detector.

Using `IOptions<T>` would add startup validation and make code-level
`Configure<T>` / `PostConfigure<T>` overrides work at the Options layer, but the
practical benefit is small for these settings: values are consumed once at
startup and cannot change an already-constructed provider. The code-override
story is already covered by `IConfiguration` source ordering (adding a
higher-priority source after the YAML source).

If future settings use `IOptions<T>` internally, `PostConfigure<T>` would then
take precedence over YAML-supplied values, which is the expected .NET idiom
(code beats configuration).

### Runtime (dynamic) disabling

The `disabled` flag is evaluated once, when the provider is constructed. There
is no mechanism to flip a live provider from a real implementation to a no-op at
runtime. This matches the OTel specification (`OTEL_SDK_DISABLED` is read at
initialization only).

Once a real provider is built, its listener wiring is fixed. The closest
approximation is replacing the sampler with `AlwaysOff` at runtime, which still
leaves processors and exporters running. For runaway instrumentation, prefer
exporter timeouts, bounded batch queues, and process restart.

### Environment-variable substitution ordering

The OTel spec states that node types must be interpreted *after* environment
variable substitution.

This implementation parses YAML first (YamlDotNet RepresentationModel preserves
scalar literals without type conversion), applies substitution to those strings,
then interprets types explicitly (for example `bool.TryParse` for `disabled`).
Outcomes are semantically equivalent for supported scalar fields today, with a
stronger guarantee that env vars cannot inject YAML structure because
substitution runs on already-tokenised scalar nodes.

### Empty-string and null semantics

When an environment variable is unset and has no default, the spec replaces the
reference with an empty string, then applies YAML 1.2 Core Schema type
resolution - a plain empty scalar becomes `null`. Quoted empty strings remain
`""`.

All YAML 1.2 core schema null spellings - plain empty, `~`, `null`, `Null`, and
`NULL` - are treated as **present-null**. Quoted variants (e.g. `"null"`) remain
strings.

For scalar fields where the key is present but the value is unusable (malformed
node type, invalid parse), the parser selects **present-null** rather than
**absent**, because the key appeared in the document and `nullBehavior` applies
at Create time.

**Post-substitution null resolution:** because the spec requires that YAML type
resolution runs *after* substitution, a plain (unquoted) scalar whose
environment variable resolves to one of the null spellings (`null`, `Null`,
`NULL`, `~`) is treated as YAML null. For example:

```yaml
resource:
  attributes:
    - name: my.attr
      value: ${MY_VAR}   # plain scalar
```

If `MY_VAR=null` the attribute is skipped (same as writing `value: null`).
If you need the literal string `"null"`, use a quoted scalar: `value: "${MY_VAR}"`.
The `InvalidResourceAttribute` EventSource event (Event ID 3) is emitted when an
attribute is skipped for this reason.

### Resource attribute name validation

Names containing `,` or `=` are skipped and Event 3 is emitted; these characters
would corrupt the `key=value,key=value` flat format consumed by
`OtelEnvResourceDetector`. All other names that do not follow the OTel attribute
naming convention (`[a-zA-Z_][-a-zA-Z0-9_.]*`) are emitted verbatim and Event 22
(`ResourceAttributeNameNotCompliant`) is emitted as a warning. This is a
.NET-projection constraint: the specification accepts attribute names verbatim
because they build typed resource objects rather than serializing to the env-var
format.

### Empty configuration file

A file containing zero YAML documents is a no-op in overlay mode and does not
require a `file_format` field (unlike a non-empty document, which still
validates `file_format`). Event 23 (`EmptyConfigurationFile`) is emitted at
informational level so listeners can observe the intentional no-op.

## Provide feedback

Please provide feedback on [issue #6380](https://github.com/open-telemetry/opentelemetry-dotnet/issues/6380)
if you are using or evaluating declarative configuration in your application.

Any feedback will help inform decisions about when to expose the API as stable
and what the final surface should look like.
