# Troubleshooting OpenTelemetry .NET

<details>
<summary>Table of Contents</summary>

* [Self-diagnostics](#self-diagnostics)
* [Enable with environment variables](#enable-with-environment-variables)
* [Enable in code](#enable-in-code)
* [Configuration reference](#configuration-reference)
  * [Options](#options)
  * [Environment variables](#environment-variables)
  * [Log levels](#log-levels)
* [Reading the output](#reading-the-output)
  * [File preamble](#file-preamble)
  * [Log entries](#log-entries)
  * [File naming and rollover](#file-naming-and-rollover)
* [What to include in a bug report](#what-to-include-in-a-bug-report)
* [Environment variable disclosure](#environment-variable-disclosure)
* [Changing the configuration at runtime](#changing-the-configuration-at-runtime)
* [Legacy self-diagnostics mechanism](#legacy-self-diagnostics-mechanism)

</details>

## Self-diagnostics

Every component shipped from the OpenTelemetry .NET repository reports its own
internal state - dropped measurements, rejected configuration, failed exports,
and so on - through
[EventSource](https://learn.microsoft.com/dotnet/api/system.diagnostics.tracing.eventsource).
The `EventSource` names all begin with `OpenTelemetry-`, for example
`OpenTelemetry-Sdk`, `OpenTelemetry-Api`, and
`OpenTelemetry-Exporter-OpenTelemetryProtocol`.

Those events can be captured with general-purpose tools such as
[PerfView](https://github.com/microsoft/perfview) or
[dotnet-trace](https://learn.microsoft.com/dotnet/core/diagnostics/dotnet-trace),
but the SDK also ships a **self-diagnostics** facility that subscribes to all of
them for you and writes formatted, human-readable entries to a rolling log file,
to the console, or to both.

> [!IMPORTANT]
> Self-diagnostics exists to diagnose the SDK itself. It is **not** a telemetry
> pipeline: entries are never exported, sampled, enriched with resource
> attributes, or turned into OpenTelemetry log records. Do not route application
> logging through it and do not treat its files as a durable log store. Use
> `ILogger` together with an exporter for application telemetry.

Self-diagnostics is off by default. Every sink is disabled until you enable one,
so a stock SDK writes nothing, anywhere.

## Enable with environment variables

The fastest way to get output requires no code change. Set the variables before
the process starts.

Write to rolling log files in `./otel-logs`:

```sh
export OTEL_DOTNET_SELF_DIAGNOSTICS_LOG_DIRECTORY=./otel-logs
```

```powershell
$env:OTEL_DOTNET_SELF_DIAGNOSTICS_LOG_DIRECTORY = "c:\otel-logs"
```

Setting the directory is all that is needed - it enables the file sink on its
own. The directory is created if it does not exist.

Write to the console instead, and lower the level so that debug entries are
included:

```sh
export OTEL_DOTNET_SELF_DIAGNOSTICS_SINKS=stdout,stderr
export OTEL_LOG_LEVEL=debug
```

```powershell
$env:OTEL_DOTNET_SELF_DIAGNOSTICS_SINKS = "stdout,stderr"
$env:OTEL_LOG_LEVEL = "debug"
```

Turn everything off again, overriding whatever else is set:

```sh
export OTEL_DOTNET_SELF_DIAGNOSTICS_SINKS=none
```

> [!TIP]
> The level defaults to `Warning`, which is intended to be safe to leave enabled
> continuously. Raise it only while investigating something:
>
> ```sh
> export OTEL_DOTNET_SELF_DIAGNOSTICS_LOG_DIRECTORY=/var/log/otel
> export OTEL_LOG_LEVEL=debug
> ```
>
> Note this default diverges from the OpenTelemetry specification, which defaults
> `OTEL_LOG_LEVEL` to `info`. See [Log levels](#log-levels) for the reasoning.

Only `OTEL_LOG_LEVEL` is shared with the wider OpenTelemetry ecosystem, and it
activates nothing on its own - with no sink selected the SDK stays silent at any
level. The variables that actually turn output on are namespaced to this
feature, so they cannot collide with the configuration of the .NET
auto-instrumentation agent's own logging. If you
configure both the SDK and the agent to log, you get both outputs.

## Enable in code

`SelfDiagnosticsOptions` is configured through the standard .NET options
pipeline, so anything that can register an `IConfigureOptions<T>` can configure
it. Values set in code override the environment variable defaults.

When using a host:

```csharp
using Microsoft.Extensions.Logging;
using OpenTelemetry.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SelfDiagnosticsOptions>(options =>
{
    options.MinimumLevel = LogLevel.Debug;
    options.LogDirectory = "/var/log/otel";
    options.FileSizeLimitKilobytes = 4096;
    options.MaxRetainedFiles = 5;
});

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddOtlpExporter());
```

When creating the SDK manually with `OpenTelemetrySdk.Create`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Diagnostics;

using var sdk = OpenTelemetrySdk.Create(builder =>
{
    builder.Services.Configure<SelfDiagnosticsOptions>(options =>
    {
        options.MinimumLevel = LogLevel.Debug;
        options.LogToStderr = true;
    });

    builder.WithTracing(tracing => tracing.AddOtlpExporter());
});
```

When creating an individual provider with the `Sdk.Create*ProviderBuilder`
APIs, use `ConfigureServices`:

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .ConfigureServices(services => services.Configure<SelfDiagnosticsOptions>(
        options => options.LogToStdout = true))
    .AddOtlpExporter()
    .Build();
```

Binding to a configuration section works too, and is what makes runtime
reconfiguration possible (see [Changing the configuration at
runtime](#changing-the-configuration-at-runtime)). The section name is yours to
choose; the SDK does not define one:

```csharp
builder.Services.Configure<SelfDiagnosticsOptions>(
    builder.Configuration.GetSection("OpenTelemetry:SelfDiagnostics"));
```

```json
{
  "OpenTelemetry": {
    "SelfDiagnostics": {
      "MinimumLevel": "Debug",
      "LogDirectory": "/var/log/otel",
      "FileSizeLimitKilobytes": 4096,
      "MaxRetainedFiles": 5,
      "EnvironmentVariables": "KnownSafeValues"
    }
  }
}
```

> [!NOTE]
> Self-diagnostics configuration is process-global rather than per-provider, so
> configuring it once is enough. If a process builds several providers, the most
> recently built one that configured a sink owns the configuration until it is
> disposed.

## Configuration reference

### Options

`OpenTelemetry.Diagnostics.SelfDiagnosticsOptions`:

| Property | Type | Default | Description |
| -------- | ---- | ------- | ----------- |
| `MinimumLevel` | `Microsoft.Extensions.Logging.LogLevel` | `Warning` | Entries below this level are discarded. `LogLevel.None` discards everything. The default diverges from the OpenTelemetry specification; see [Log levels](#log-levels). |
| `LogDirectory` | `string?` | `null` | Directory for the rolling log files. Setting it enables the file sink; `null` or empty disables it. Created if missing. |
| `FileSizeLimitKilobytes` | `int` | `10240` (10 MiB) | Size at which the current file is closed and a new one opened. Files are never truncated. A value less than or equal to `0` disables size-based rollover (unlimited). Positive values are not clamped to a minimum; a file may exceed the limit by its preamble and the entry that crosses the boundary. |
| `MaxRetainedFiles` | `int` | `0` (unlimited) | Number of log files kept. The oldest is pruned when opening a new file would exceed a positive limit. Values less than or equal to `0` disable automatic pruning. |
| `LogToStdout` | `bool` | `false` | Enables the console sink, writing to standard output. |
| `LogToStderr` | `bool` | `false` | Enables the console sink, writing to standard error. |
| `EnvironmentVariables` | `OpenTelemetry.Diagnostics.EnvironmentVariableLogMode` | `KnownSafeValues` | How much of the `OTEL_*` environment variable snapshot is written into the file preamble. See [Environment variable disclosure](#environment-variable-disclosure). |

Console stream routing:

* If only `LogToStdout` is set, every entry goes to standard output.
* If only `LogToStderr` is set, every entry goes to standard error.
* If **both** are set, entries at `Warning` and below go to standard output and
  entries above `Warning` (`Error` and `Critical`) go to standard error.

The file sink and the console sink are independent. Enabling both writes each
entry to both.

### Environment variables

Environment variables supply *defaults*. Any
`Configure<SelfDiagnosticsOptions>` callback runs afterwards and wins.

| Variable | Sets | Accepted values |
| -------- | ---- | --------------- |
| `OTEL_LOG_LEVEL` | `MinimumLevel` | `error`, `warn`, `info`, `debug`, `trace`, `none`, or a .NET `LogLevel` name (`Critical`, `Error`, `Warning`, `Information`, `Debug`, `Trace`, `None`). Case-insensitive. Numeric values are rejected. |
| `OTEL_DOTNET_SELF_DIAGNOSTICS_SINKS` | Which sinks are enabled | A comma-separated list of `none`, `file`, `stdout`, `stderr`, and `console`. |
| `OTEL_DOTNET_SELF_DIAGNOSTICS_LOG_DIRECTORY` | `LogDirectory` | Any absolute or relative directory path. |
| `OTEL_DOTNET_SELF_DIAGNOSTICS_ENV_VARS` | `EnvironmentVariables` | `none`, `names`, `knownsafe`, `all`. |

`OTEL_LOG_LEVEL` is a stable OpenTelemetry specification variable and is the only
one of the four shared with the wider ecosystem. It selects a severity and
nothing more: it never activates a sink, so on its own it produces no output. The
three variables that do activate output are namespaced to this feature so they
cannot collide with the .NET auto-instrumentation agent's own logging
configuration.

`OTEL_DOTNET_SELF_DIAGNOSTICS_SINKS` takes a comma-separated list, for example
`stdout`, `file,stderr`, or `stdout,stderr`. Whitespace around tokens is
tolerated, as are empty entries produced by doubled or trailing separators. The
recognised tokens are:

* `file` - the rolling file sink. Requires
  `OTEL_DOTNET_SELF_DIAGNOSTICS_LOG_DIRECTORY`; listed without a directory it
  writes nothing and produces a configuration warning in the preamble.
* `stdout` - the console sink, writing to standard output.
* `stderr` - the console sink, writing to standard error.
* `console` - an alias for `stdout,stderr`, for anyone arriving with the .NET
  auto-instrumentation agent's vocabulary.
* `none` - silence. It overrides every other token wherever it appears in the
  list, because silence is the safe reading of a contradictory value.

Whether the variable is set at all changes how the log directory is interpreted:

* **Present** - the list is authoritative. If `file` is not among the tokens, any
  `OTEL_DOTNET_SELF_DIAGNOSTICS_LOG_DIRECTORY` that was set is ignored and no
  file is written.
* **Absent** - the sink set is inferred from the log directory alone, so setting
  only `OTEL_DOTNET_SELF_DIAGNOSTICS_LOG_DIRECTORY` is enough to get file output.

A value that cannot be parsed is ignored and the default is kept, so a typo in
`OTEL_LOG_LEVEL` leaves the level at `Warning` rather than failing startup.
An unrecognised sink token is likewise ignored while the correctly-spelled tokens
in the same list still apply. Every rejected value is reported in a
`Configuration Warnings:` block in the log file preamble, so a mistake is not
silent.

For `OTEL_DOTNET_SELF_DIAGNOSTICS_ENV_VARS` the `EnvironmentVariableLogMode`
member names are also accepted, so a value copied out of code works in the
environment variable and vice versa: `knownsafevalues` and `allvalues` are
equivalent to `knownsafe` and `all`. The shorter `known` is accepted as well,
though `knownsafe` is preferred because it keeps the word that distinguishes
"values known to be safe to show" from "variables the SDK knows about".

### Log levels

`MinimumLevel` is a
[`Microsoft.Extensions.Logging.LogLevel`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.loglevel).
It is a minimum threshold: setting it to `Warning` captures `Warning`, `Error`,
and `Critical` events.

| `OTEL_LOG_LEVEL` token | `LogLevel` | Notes |
| ---------------------- | ---------- | ----- |
| `none` | `None` | Nothing is captured. |
| `error` | `Error` | |
| `warn` | `Warning` | The default. |
| `info` | `Information` | The OpenTelemetry specification default. |
| `debug` | `Debug` | Also records loaded assemblies and their versions. |
| `trace` | `Trace` | The most verbose setting. |

There is no OpenTelemetry specification token for `Critical`; use the .NET name
if you need it.

> [!NOTE]
> The default is `Warning`, which **diverges from the OpenTelemetry
> specification**, whose default for `OTEL_LOG_LEVEL` is `info`.
>
> `Information` and below are considerably more verbose, and that verbosity costs
> log volume, disk, and sink throughput without usually adding diagnostic value
> until something is actually being investigated. Since this channel is meant to
> be safe to leave enabled in production, the more economical level is the more
> useful default.
>
> To take the specification default instead:
>
> ```sh
> export OTEL_LOG_LEVEL=info
> ```
>
> When you are actively investigating something, `debug` is usually the level to
> reach for rather than `info`.
>
> Either way this only matters once a sink is enabled. With no sink configured the
> SDK is silent at any level, so `OTEL_LOG_LEVEL` on its own never produces
> output.

Underneath, the SDK's `EventSource` events carry an
[`EventLevel`](https://learn.microsoft.com/dotnet/api/system.diagnostics.tracing.eventlevel),
which is coarser than `LogLevel`. `EventLevel.Verbose` events surface as
`Debug`, so `Trace` and `Debug` currently subscribe to the same set of SDK
events. `Debug` is the level to reach for first: it additionally emits an entry
per loaded assembly with the assembly version, informational version, and
directory, which answers most "which version am I actually running" questions.

## Reading the output

### File preamble

Every log file opens with a freshly generated preamble followed by a column
header, so a single file lifted out of a directory and attached to an issue is
self-contained. Files are opened with `FileShare.Read`, so `tail -f` and similar
tools can follow a live file.

```text
=== OpenTelemetry .NET SDK self-diagnostics ===
SDK version          : 1.14.0
DateTime (UTC)       : 2026-08-06T09:14:21.8410000Z
Runtime              : .NET 10.0.0
CLR version          : 10.0.0
OS                   : Ubuntu 24.04.1 LTS
Architecture         : X64
Runtime ID           : linux-x64
Process ID           : 24680
Process name         : MyCompany.MyService
Process start time   : 2026-08-06T09:14:20.1030000Z
Process working set  : 61865984 bytes
Thread count         : 21
Process path         : /app/MyCompany.MyService
Entry assembly       : MyCompany.MyService, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null

Machine name         : my-service-prod-3
Processor count      : 8
64-bit OS            : True
64-bit process       : True
Server GC            : True
GC latency mode      : SustainedLowLatency
App base directory   : /app/
Working directory    : /app

Environment Variables (mode: KnownSafeValues):
OTEL_EXPORTER_OTLP_ENDPOINT_TRACES = [REDACTED]
OTEL_EXPORTER_OTLP_HEADERS = [REDACTED]
OTEL_RESOURCE_ATTRIBUTES = service.version=2.3.1,tenant=[REDACTED]
OTEL_SERVICE_NAME = my-service
OTEL_TRACES_SAMPLER = parentbased_traceidratio
=== end preamble ===

DateTime (UTC)                Thread  SpanId  Level         Message
```

Two things in that snapshot are worth pointing out, because they are what the
preamble is for:

* `OTEL_EXPORTER_OTLP_ENDPOINT_TRACES` is not a real variable; the correct name
  is `OTEL_EXPORTER_OTLP_TRACES_ENDPOINT`. The preamble lists the name of *every*
  `OTEL_*` variable that is set, recognised or not, so a misspelled or invented
  variable name - one of the most common misconfigurations these files exist to
  find - is visible at a glance.
* On Windows each line is also annotated with `(source: process | user |
  system)`, identifying which scope supplied the effective value.

If an environment variable was set to a value that could not be parsed, a
`Configuration Warnings:` block appears above the snapshot naming the variable,
the rejected value, and the accepted values. Options are constructed before any
sink exists, so the preamble is the only place such a failure can be reported.

The console sink writes entries only; it has no preamble.

### Log entries

Each entry is one line, plus additional lines for an exception when one is
attached. The prefix is fixed-width and padded to 60 characters so that messages
line up under the column header:

```text
[<timestamp>][<threadId>][<spanId>][<level>]  <message> {EventId} <traceparent>
```

* `<timestamp>` - UTC, round-trip (`O`) format.
* `<threadId>` - the OS thread id, zero-padded to six digits, or `------` when
  the runtime does not provide one.
* `<spanId>` - the first six hex characters of the current span id, or `------`
  when no `Activity` was current.
* `<level>` - the full `LogLevel` name.
* `{EventId: n, EventName: x}` - appended when the event carries an id or name.
* `<00-traceId-spanId-flags>` - appended when an `Activity` was current, in
  [W3C traceparent](https://www.w3.org/TR/trace-context/#traceparent-header)
  form, so an entry can be correlated with the trace it happened inside.

A worked example: a warning emitted with no ambient `Activity`, then two entries
emitted inside one, the last of which is an export failure:

```text
DateTime (UTC)                Thread  SpanId  Level         Message

[2026-08-06T09:14:22.0132451Z][000001][------][Warning]     OpenTelemetry-Sdk: Measurements from Instrument 'dotnet.gc.collections', Meter 'System.Runtime' will be ignored. Reason: 'Instrument belongs to a Meter not subscribed by the provider.'. Suggested action: 'Use AddMeter to add the Meter to the provider.' {EventId: 33, EventName: MetricInstrumentIgnored}
[2026-08-06T09:14:24.5507712Z][000014][00f067][Information] OpenTelemetry-Sdk: Activity started. Name = 'HTTP GET', Id = '00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01'. {EventId: 24, EventName: ActivityStarted}
[2026-08-06T09:14:31.4471190Z][000014][00f067][Error]       OpenTelemetry-Exporter-OpenTelemetryProtocol: Exporter failed send data to collector to https://otlp.example.com:4317 endpoint. Data will not be sent. Exception: System.Net.Http.HttpRequestException: No connection could be made because the target machine actively refused it. {EventId: 2, EventName: FailedToReachCollector} <00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01>
```

The message itself is prefixed with the name of the `EventSource` that produced
it, which tells you which component to look at: `OpenTelemetry-Sdk` for the SDK,
`OpenTelemetry-Api` for the API, and `OpenTelemetry-Exporter-*` for exporters.

When an entry carries a .NET exception object rather than a pre-formatted
message, the full `Exception.ToString()` - message, stack trace, and inner
exceptions - follows on the lines after the entry.

### File naming and rollover

Files are named:

```text
otel-dotnet-{pid}-{processName}-{processStartTime:yyyyMMdd-HHmmss}-{index}.log
```

The index increases monotonically for the lifetime of the process. When the
current file reaches `FileSizeLimitKilobytes` it is closed and index `+ 1` is
opened with its own preamble; nothing is ever truncated or overwritten in place.
When `MaxRetainedFiles` is positive, opening a new file after that limit is
reached deletes the oldest. Its default value of `0` disables automatic pruning,
so every rolled file is retained until it is removed externally. Configure a
positive retention count or external cleanup in long-running deployments to
prevent self-diagnostics from exhausting disk space. Setting
`FileSizeLimitKilobytes` to `0` instead produces a single unbounded file.

## What to include in a bug report

Self-diagnostics output is the single most useful attachment on an issue. To
collect it, reproduce the problem with the level lowered and the file sink on:

```sh
export OTEL_LOG_LEVEL=debug
export OTEL_DOTNET_SELF_DIAGNOSTICS_LOG_DIRECTORY=./otel-logs
```

Then attach:

* The complete log file or files from the directory, not an excerpt. The
  preamble carries the SDK version, runtime, OS, architecture, process details,
  GC mode, and the `OTEL_*` snapshot, and the `Debug` level adds the list of
  loaded assemblies with their versions. Together those answer most of the
  questions a maintainer would otherwise have to ask.
* What you expected to see and where - which spans, metrics, or log records are
  missing, and in which backend.
* The wall-clock time of the failure, so entries can be matched to it.
* A minimal reproduction if you have one.

Review the file before attaching it. Values are redacted by default (see
[Environment variable disclosure](#environment-variable-disclosure)), but
messages emitted by the SDK can still contain endpoints, header names, resource
attributes, and exception details from your deployment.

If the redacted snapshot hides the very setting under investigation, and you are
able to share it, re-run the reproduction with full disclosure:

```sh
export OTEL_DOTNET_SELF_DIAGNOSTICS_ENV_VARS=all
```

## Environment variable disclosure

The file preamble contains a snapshot of the `OTEL_*` environment variables the
process was started with. Nothing outside the `OTEL_*` namespace is ever
captured. How much of that snapshot is written is controlled by the
`EnvironmentVariables` option, or by
`OTEL_DOTNET_SELF_DIAGNOSTICS_ENV_VARS`:

| Mode | Variable value | Behaviour |
| ---- | -------------- | --------- |
| `None` | `none` | The section is omitted entirely. |
| `Names` | `names` | Variable names only. No values at all. |
| `KnownSafeValues` | `knownsafe` | Names are always shown. A value is shown only for a variable the SDK recognises as safe to disclose; every other value becomes `[REDACTED]`. This is the default. |
| `AllValues` | `all` | Every value is written verbatim. |

The modes are listed in increasing order of disclosure, and the default sits
deliberately in the middle rather than at either end.

Names and values are treated differently. A variable *name* is a schema
identifier rather than a credential, so every mode above `None` lists the name of
every `OTEL_*` variable that is set. A variable *value* is the sensitive part.

Values are shown by allowlist, never by denylist. A denylist would have to
enumerate every variable that must be hidden, and would therefore leak any
variable nobody has classified yet - a new SDK setting, a vendor-specific
variable, a variable a distribution adds downstream. Under `KnownSafeValues` an
unclassified variable loses its value and keeps only its name, which is enough to
tell a maintainer that the variable was set without exposing what it was set to.
`OTEL_EXPORTER_OTLP_HEADERS` is redacted because it is absent from the safe list,
not because anyone remembered to add it to a danger list.

Two free-form variables are partially classifiable and are handled specially
under `KnownSafeValues`:

* Endpoint variables such as `OTEL_EXPORTER_OTLP_ENDPOINT` are reduced to their
  authority (scheme, host, port). The userinfo, path, query, and fragment are
  dropped, because those are what carry tokens in signed-URL and
  API-key-in-query deployments.
* `OTEL_RESOURCE_ATTRIBUTES` is redacted per key, so well-known identifiers such
  as `service.name`, `service.version`, and the `deployment.*`, `host.*`, and
  `k8s.*` families survive while user-supplied keys keep their names and lose
  their values.

Any value that spans multiple lines or opens with PEM armour is redacted
regardless of mode below `AllValues`, on the basis that no path, endpoint, or
scalar setting legitimately looks like that - it is key material pasted where a
file reference was expected.

`AllValues` is the deliberate opt-in for when a support engagement needs the
full picture and you have decided the output can be shared. Treat a file
produced under `AllValues` as a secret: it can contain authentication headers,
signed URLs, and API keys embedded in endpoints. Prefer enabling it for one
reproduction run rather than leaving it on.

`Names` and `None` narrow the disclosure further when even variable names are
considered sensitive, at the cost of making the file much less useful for
diagnosis.

If a value you need is redacted under `KnownSafeValues` and it is not actually
sensitive, that is a classification gap worth reporting at
[opentelemetry-dotnet issues](https://github.com/open-telemetry/opentelemetry-dotnet/issues)
so the allowlist can be widened for everyone.

## Changing the configuration at runtime

The options are consumed through
[`IOptionsMonitor<SelfDiagnosticsOptions>`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.ioptionsmonitor-1),
so any configuration source that emits change notifications can reconfigure
self-diagnostics in a running process without rebuilding the provider or
restarting. That includes a file-based configuration provider registered with
`reloadOnChange: true` (the default for `appsettings.json` under the .NET host)
and remote configuration agents such as
[OpAMP](https://opentelemetry.io/docs/specs/opamp/) that push updates into an
`IConfiguration` source.

This is what makes it practical to turn diagnostics on during an incident:
lower `MinimumLevel` to `Debug`, get the output, and put it back, all without a
deployment.

To enable this, bind `SelfDiagnosticsOptions` to a configuration section (see
[Enable in code](#enable-in-code)):

```csharp
builder.Services.Configure<SelfDiagnosticsOptions>(
    builder.Configuration.GetSection("OpenTelemetry:SelfDiagnostics"));
```

Then edit `appsettings.json` while the process is running:

```json
{
  "OpenTelemetry": {
    "SelfDiagnostics": {
      "MinimumLevel": "Debug",
      "LogDirectory": "/var/log/otel"
    }
  }
}
```

Everything is reloadable: the level, which sinks are active, the directory, the
size limit, and the retention count. Changing the directory closes the current
file and opens a new one, with a fresh preamble, in the new location.

> [!NOTE]
> Environment variables and `Configure<SelfDiagnosticsOptions>` callbacks do
> **not** reload. Neither source emits change notifications, so a value from
> either is fixed for the lifetime of the process. Bind to a reloading
> configuration section if you want runtime control.

## Legacy self-diagnostics mechanism

Earlier versions of the SDK were configured by dropping an
`OTEL_DIAGNOSTICS.json` file into the working directory of the process. The SDK
re-read it every ten seconds and wrote into a fixed-size, memory-mapped circular
buffer named `ExecutableName.ProcessId.log`:

```json
{
    "LogDirectory": ".",
    "FileSize": 32768,
    "LogLevel": "Warning",
    "FormatMessage": "true"
}
```

* `LogDirectory` - absolute, or relative to the working directory.
* `FileSize` - the log file size in KiB, clamped to the range
  `[1024, 131072]`. The file never grows beyond it and is overwritten
  circularly, so it can contain trailing `NUL` characters and interleaved old
  content.
* `LogLevel` - an
  [`EventLevel`](https://learn.microsoft.com/dotnet/api/system.diagnostics.tracing.eventlevel)
  name, not a `LogLevel` name.
* `FormatMessage` - whether `{0}`-style placeholders in messages are replaced
  with their argument values. Defaults to `false`.

Deleting the file disables it. An unparsable file is treated as invalid and no
output is produced.

That mechanism still works and is unchanged, but it is superseded by everything
described above and **will be removed in a future major version**. It runs
independently of the mechanism on this page, so if you configure both you will
get two copies of every event, in two different formats.

Prefer the new mechanism for anything new. The remaining reason to reach for the
legacy file is that it needs no restart and no code change on an already-running
process that has no reloading configuration source.
