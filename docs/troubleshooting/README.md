# Troubleshooting OpenTelemetry .NET

<details>
<summary>Table of Contents</summary>

* [Self-diagnostics](#self-diagnostics)
* [Enable with environment variables](#enable-with-environment-variables)
* [Enable with code](#enable-with-code)
* [Configuration reference](#configuration-reference)
  * [Options](#options)
  * [Environment variables](#environment-variables)
  * [Default log directory](#default-log-directory)
  * [Log levels](#log-levels)
* [Reading the output](#reading-the-output)
  * [File preamble](#file-preamble)
  * [Log entries](#log-entries)
  * [File naming and rollover](#file-naming-and-rollover)
* [What to include in a bug report](#what-to-include-in-a-bug-report)
* [Environment variable disclosure](#environment-variable-disclosure)
* [Changing the configuration at
  runtime](#changing-the-configuration-at-runtime)
* [Legacy self-diagnostics mechanism](#legacy-self-diagnostics-mechanism)

</details>

> [!NOTE]
> As at 1.17.0, the self-diagnostics features described on this page are
> not yet released. See [Legacy self-diagnostics
> mechanism](#legacy-self-diagnostics-mechanism) for the mechanism available in
> current stable releases.

<!-- TODO: update the NOTE and the legacy mechanism section once a release
ships this feature. -->

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
but the SDK also ships a self-diagnostics facility that subscribes to all of
them for you and writes formatted, human-readable entries to a rolling log file,
to the console, or to both.

> [!IMPORTANT]
> Self-diagnostics exists to diagnose the SDK itself. It is **not**
> a telemetry pipeline: entries are never exported, sampled, enriched with
> resource attributes, or turned into OpenTelemetry log records. Do not route
> application logging through it and do not treat its files as a durable log
> store. Use `ILogger` together with an exporter for application telemetry.

Self-diagnostics is off by default. Every sink is disabled until you enable one,
so a stock SDK writes nothing unless configured.

## Enable with environment variables

The easiest way to get output requires no code changes. Set the variables before
the process starts.

Write to rolling log files in a chosen directory:

```sh
export OTEL_DOTNET_SELF_DIAGNOSTICS_LOG_DIRECTORY=./otel-logs
```

```powershell
$env:OTEL_DOTNET_SELF_DIAGNOSTICS_LOG_DIRECTORY = "c:\otel-logs"
```

Setting the directory is all that is needed - it enables the file sink on its
own. The directory is created if it does not exist. The process must have
permission to create and write files there.

Write to rolling log files using the default platform location when one is
available:

```sh
export OTEL_DOTNET_SELF_DIAGNOSTICS_SINKS=file
```

```powershell
$env:OTEL_DOTNET_SELF_DIAGNOSTICS_SINKS = "file"
```

See [Default log directory](#default-log-directory) for the exact paths.

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
> The level defaults to `Warning`, which is intended to be safe to leave
> enabled continuously. Raise it only while investigating something:
>
> ```sh
> export OTEL_DOTNET_SELF_DIAGNOSTICS_LOG_DIRECTORY=/var/log/otel
> export OTEL_LOG_LEVEL=debug
> ```

<!-- -->

> [!NOTE]
> The default level diverges from the OpenTelemetry specification, where
> `OTEL_LOG_LEVEL` is set to `info`. See [Log levels](#log-levels) for the
> reasoning.

Only `OTEL_LOG_LEVEL` is shared with the wider OpenTelemetry ecosystem, and it
activates nothing on its own. With no sink selected the SDK logs nothing at any
level. The variables that turn output on are specific to this feature, so they
cannot collide with the [.NET auto-instrumentation
agent's](https://github.com/open-telemetry/opentelemetry-dotnet-instrumentation)
own diagnostic logging configuration. If you configure both the SDK and the
agent to log, you get both outputs.

## Enable with code

`SelfDiagnosticsOptions` is configured through the standard .NET options
pipeline. Values set in code override the environment variable defaults.

When using ASP.NET Core or a .NET Generic Host application:

```csharp
using Microsoft.Extensions.Logging;
using OpenTelemetry;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SelfDiagnosticsOptions>(options =>
{
    options.MinimumLevel = LogLevel.Debug;
    options.LogDirectory = SelfDiagnosticsOptions.GetDefaultLogDirectory()
        ?? "/var/log/otel";
    options.FileSizeLimitKilobytes = 4096;
    options.MaxRetainedFiles = 5;
});

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddOtlpExporter());
```

When creating the SDK manually with `OpenTelemetrySdk.Create` (console apps,
AWS Lambda, etc.):

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;

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

When using the individual provider builder APIs (`Sdk.Create*ProviderBuilder`),
use `ConfigureServices`:

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
> Self-diagnostics configuration is process-global rather than
> per-provider, so configuring it once is enough. If a process builds several
> providers, the most recently built one that configured a sink owns the
> configuration until it is disposed.

## Configuration reference

When a sink is active, each log entry is one formatted line. The file sink also
writes a preamble at the start of each file containing the SDK version, runtime
details, and a snapshot of `OTEL_*` environment variables. See [Reading the
output](#reading-the-output) for the full format.

### Options

`OpenTelemetry.SelfDiagnosticsOptions`:

| Property | Type | Default | Description |
| -------- | ---- | ------- | ----------- |
| `MinimumLevel` | `Microsoft.Extensions.Logging.LogLevel` | `Warning` | Entries below this level are discarded. `LogLevel.None` discards everything. The default diverges from the OpenTelemetry specification; see [Log levels](#log-levels). |
| `LogDirectory` | `string?` | `null` | Directory for the rolling log files. Set it to turn file logging on; leave it empty to keep file logging off. The directory is created if needed. When `OTEL_DOTNET_SELF_DIAGNOSTICS_SINKS` asks for `file` without a directory, the SDK tries the [default log directory](#default-log-directory). From code, use `SelfDiagnosticsOptions.GetDefaultLogDirectory()` for the same path. |
| `FileSizeLimitKilobytes` | `int` | `10240` (10 MiB) | Size at which the current file is closed and a new one opened. Files are never truncated. A value less than or equal to `0` disables size-based rollover (unlimited). Positive values are not clamped to a minimum; a file may exceed the limit by its preamble and the entry that crosses the boundary. |
| `MaxRetainedFiles` | `int` | `10` | Number of log files kept. The oldest is pruned when opening a new file would exceed a positive limit. Values less than or equal to `0` disable automatic pruning and retain every rolled file indefinitely. |
| `LogToStdout` | `bool` | `false` | Enables the console sink, writing to standard output. |
| `LogToStderr` | `bool` | `false` | Enables the console sink, writing to standard error. |
| `EnvironmentVariables` | `OpenTelemetry.EnvironmentVariableLogMode` | `KnownSafeValues` | How much of the `OTEL_*` environment variable snapshot is written into the file preamble. See [Environment variable disclosure](#environment-variable-disclosure). |

Console stream routing:

* If only `LogToStdout` is set, every entry goes to standard output.
* If only `LogToStderr` is set, every entry goes to standard error.
* If **both** are set, entries at `Warning` and below go to standard output and
  entries above `Warning` (`Error` and `Critical`) go to standard error.

The file sink and the console sink are independent. Enabling both writes each
entry to both.

### Environment variables

Environment variables supply *defaults*. Any `Configure<SelfDiagnosticsOptions>`
callback runs afterwards and takes precedence.

| Variable | Sets | Accepted values |
| -------- | ---- | --------------- |
| `OTEL_LOG_LEVEL` | `MinimumLevel` | OTel log level names (`error`, `warn`, `info`, `debug`, `trace`, `none`) or .NET `LogLevel` names. Case-insensitive. See [Log levels](#log-levels). |
| `OTEL_DOTNET_SELF_DIAGNOSTICS_SINKS` | Which sinks are enabled | A comma-separated list of `file`, `stdout`, `stderr`, and `console`. Use `none` alone to disable all output. |
| `OTEL_DOTNET_SELF_DIAGNOSTICS_LOG_DIRECTORY` | `LogDirectory` | Any absolute or relative directory path. |
| `OTEL_DOTNET_SELF_DIAGNOSTICS_ENV_VARS` | `EnvironmentVariables` | `none`, `names`, `knownsafe`, `all`. |

`OTEL_LOG_LEVEL` is a stable OpenTelemetry specification variable and is the
only one shared with the wider ecosystem. It selects a severity and nothing
more: it never activates a sink, so on its own it produces no output.

`OTEL_DOTNET_SELF_DIAGNOSTICS_SINKS` takes a comma-separated list, for example
`stdout`, `file,stderr`, or `stdout,stderr`. Whitespace around tokens is
ignored. The recognised tokens are:

* `file` - the rolling file sink. Uses
  `OTEL_DOTNET_SELF_DIAGNOSTICS_LOG_DIRECTORY` when set; otherwise the SDK
  tries the [default log directory](#default-log-directory).
* `stdout` - the console sink, writing to standard output.
* `stderr` - the console sink, writing to standard error.
* `console` - an alias for `stdout,stderr`, for anyone arriving with the .NET
  auto-instrumentation agent's vocabulary.
* `none` - disables all sinks, regardless of what else is in the list.

The following table summarises all combinations of the two variables and their
effect on file output:

| `OTEL_DOTNET_SELF_DIAGNOSTICS_LOG_DIRECTORY` | `OTEL_DOTNET_SELF_DIAGNOSTICS_SINKS` | File output |
| -------------------------------------------- | ------------------------------------ | ----------- |
| set | absent | active at the specified path |
| set | includes `file` | active at the specified path |
| set | present, `file` absent | **off** - `SINKS` is the explicit override |
| absent | absent | off |
| absent | includes `file` | active at the [default path](#default-log-directory) |
| absent | present, `file` absent | off |
| any | `none` | **off** - overrides all other tokens |

The third row lets an individual service opt out when a deployment sets a log
directory globally across many services. Set
`OTEL_DOTNET_SELF_DIAGNOSTICS_SINKS` to a list that does not include `file` (for
example `stdout`) and no file is written, even though the directory is set.

Invalid values are ignored and the default is kept. An unrecognised sink token
is likewise ignored while any valid tokens in the same list still apply. All
rejected values are reported in a `Configuration Warnings:` block in the file
preamble.

### Default log directory

When `OTEL_DOTNET_SELF_DIAGNOSTICS_SINKS` includes `file` but
`OTEL_DOTNET_SELF_DIAGNOSTICS_LOG_DIRECTORY` is not set, the SDK tries to use a
platform-appropriate per-user directory that is normally writable without
elevated privileges:

| Platform | Default path |
| -------- | ------------ |
| Windows | `%LOCALAPPDATA%\OpenTelemetry\dotnet-diagnostics` |
| macOS | `~/Library/Logs/OpenTelemetry/dotnet-diagnostics` |
| Linux / other Unix | `$XDG_STATE_HOME/opentelemetry/dotnet-diagnostics` if `$XDG_STATE_HOME` is set to an absolute path; otherwise `~/.local/state/opentelemetry/dotnet-diagnostics` |

The directory is created on first write. Setting
`OTEL_DOTNET_SELF_DIAGNOSTICS_LOG_DIRECTORY` always takes precedence over the
default. From code, call `SelfDiagnosticsOptions.GetDefaultLogDirectory()` for
the same path. If no per-user directory can be resolved, the file sink stays
off and the SDK writes a warning to standard error; set an explicit directory
in that case. As with any file output, the process must have permission to
create and write to the resolved directory.

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
> `Information` and below are considerably more verbose, and that verbosity
> costs log volume, disk, and sink throughput without usually adding diagnostic
> value until something is actually being investigated. Since this channel is
> meant to be safe to leave enabled in production, the more economical level is
> the more useful default.
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
> Either way this only matters once a sink is enabled. With no sink configured
> the SDK produces no log entries at any level, so `OTEL_LOG_LEVEL` on its own
> never produces output.

The SDK events are carried by `EventSource`, which uses a coarser granularity
than `LogLevel`. At present, both `Trace` and `Debug` subscribe to the same set
of events. Start with `Debug`; it adds one entry per loaded assembly showing the
version, informational version, and directory - the quickest way to confirm
which build is actually running.

## Reading the output

### File preamble

Every log file opens with a freshly generated preamble followed by a column
header, so a single file lifted out of a directory is self-contained.

```text
=== OpenTelemetry .NET SDK self-diagnostics ===
SDK version          : 1.18.0-rc.1
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
Log directory        : /home/app/.local/state/opentelemetry/dotnet-diagnostics
Dynamic code         : True
GC heap committed    : 42 MiB
GC memory limit      : 8192 MiB

Runtime environment variables:
(none set)

Environment Variables (mode: KnownSafeValues):
OTEL_EXPORTER_OTLP_ENDPOINT_TRACES = [REDACTED]
OTEL_EXPORTER_OTLP_HEADERS = [REDACTED]
OTEL_RESOURCE_ATTRIBUTES = service.version=2.3.1,tenant=[REDACTED]
OTEL_SERVICE_NAME = my-service
OTEL_TRACES_SAMPLER = parentbased_traceidratio
=== end preamble ===

DateTime (UTC)                Thread  SpanId  Level         Message
```

Three things in that snapshot are worth pointing out:

* `OTEL_EXPORTER_OTLP_ENDPOINT_TRACES` is not a real variable; the correct name
  is `OTEL_EXPORTER_OTLP_TRACES_ENDPOINT`. The preamble lists the name of
  *every* `OTEL_*` variable that is set, recognised or not, so a misspelled or
  invented variable name - one of the most common misconfigurations these files
  exist to find - is visible at a glance.
* `Runtime environment variables:` lists a fixed set of CLR profiler and
  startup variables (`CORECLR_ENABLE_PROFILING`, `DOTNET_STARTUP_HOOKS`,
  `ASPNETCORE_HOSTINGSTARTUPASSEMBLIES`, and others). They appear regardless of
  whether any of them are set. If an auto-instrumentation agent is attached, the
  profiler GUID and path show up here, confirming which agent version is active.
* On Windows each line in the `Environment Variables` section is annotated with
  `(source: process | user | system)`, identifying which scope supplied the
  effective value.

If an environment variable was set to a value that could not be parsed, a
`Configuration Warnings:` block appears above the snapshot naming the variable,
the rejected value, and the accepted values. Options are constructed before any
sink exists, so the preamble is the only place such a failure can be reported.

The console sink writes entries only; it has no preamble.

### Log entries

Each entry is one line, plus additional lines for an exception when one is
attached. The prefix is fixed-width and padded so that messages line up under
the column header:

```text
[<timestamp>][<threadId>][<spanId>][<level>]  <message> {EventId} <traceparent>
```

* `<timestamp>` - UTC, [round-trip (`O`)
  format](https://learn.microsoft.com/dotnet/standard/base-types/standard-date-and-time-format-strings#Roundtrip).
* `<threadId>` - the thread id, zero-padded to six digits, or `------` when
  the runtime does not provide one.
* `<spanId>` - the first six hex characters of the current span id, or `------`
  when no `Activity` was available at the point the diagnostic event was raised.
* `<level>` - the full `LogLevel` name.
* `{EventId: n, EventName: x}` - appended when the event carries an id or name.
* `<00-traceId-spanId-flags>` - appended when an `Activity` was present at the
  point the diagnostic event was raised, in [W3C
  traceparent](https://www.w3.org/TR/trace-context/#traceparent-header) form, so
  an entry can be correlated with the trace it happened inside.

Here's an example: a warning emitted with no ambient `Activity`, then two
entries emitted inside one, the last of which is an export failure:

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
otel-dotnet-{pid}-{processName}-{processStartTime:yyyyMMdd-HHmmss.fffffff}-{index}.log
```

The index increases monotonically for the lifetime of the process. When the
current file reaches `FileSizeLimitKilobytes` it is closed and index `+ 1` is
opened with its own preamble; nothing is ever truncated or overwritten in place.
When `MaxRetainedFiles` is positive, opening a new file after that limit is
reached deletes the oldest. Setting `FileSizeLimitKilobytes` to `0` produces a
single unbounded file.

> [!NOTE]
> `MaxRetainedFiles` defaults to `10`, which bounds automatic
> pruning to a modest history. In long-running deployments, adjust the
> retention limit as needed, or set it to `0` (or less) with external cleanup
> arranged if unlimited retention is required.

## What to include in a bug report

When reporting an issue, alongside any full reproduction steps, a
self-diagnostics log collected at `Debug` level is extremely useful to aid
diagnosis. Collect one by running the reproduction with:

```sh
export OTEL_LOG_LEVEL=debug
export OTEL_DOTNET_SELF_DIAGNOSTICS_SINKS=file
```

This writes to the [default log directory](#default-log-directory) for your
platform. To control the location or the number of retained files:

```csharp
builder.Services.Configure<SelfDiagnosticsOptions>(options =>
{
    options.MinimumLevel = LogLevel.Debug;
    options.LogDirectory = SelfDiagnosticsOptions.GetDefaultLogDirectory();
    options.MaxRetainedFiles = 5;
});
```

If you prefer to set everything via environment variables:

```sh
export OTEL_LOG_LEVEL=debug
export OTEL_DOTNET_SELF_DIAGNOSTICS_LOG_DIRECTORY=./otel-logs
```

Include the log contents in your [issue
report](https://github.com/open-telemetry/opentelemetry-dotnet/issues/new/choose),
along with:

* A minimal reproduction if you have one.
* What you expected to happen and what happened instead.
* The wall-clock time of the failure, to help correlate log entries.

> [!IMPORTANT]
> Review the log before sharing it. Values are redacted by default
> (see [Environment variable disclosure](#environment-variable-disclosure)), but
> log messages can still contain paths, endpoints, header names,
> resource attributes, and exception details from your deployment.

If the redacted snapshot hides the setting under investigation, you can enable
full disclosure for the reproduction run:

```sh
export OTEL_DOTNET_SELF_DIAGNOSTICS_ENV_VARS=all
```

## Environment variable disclosure

The file preamble contains a snapshot of the `OTEL_*` environment variables the
process was started with. Nothing outside the `OTEL_*` namespace is ever
captured. How much of that snapshot is written is controlled by the
`EnvironmentVariables` option, or by `OTEL_DOTNET_SELF_DIAGNOSTICS_ENV_VARS`:

| Mode | Variable value | Behaviour |
| ---- | -------------- | --------- |
| `None` | `none` | The section is omitted entirely. |
| `Names` | `names` | Variable names only. No values at all. |
| `KnownSafeValues` | `knownsafe` | Names are always shown. A value is shown only for a variable the SDK recognises as safe to disclose; every other value becomes `[REDACTED]`. This is the default. |
| `AllValues` | `all` | Every value is written verbatim. |

The modes are listed in increasing order of disclosure. The default,
`KnownSafeValues`, is intended to be useful for diagnosis without leaking
potentially sensitive values into log files.

Values are shown by allowlist only. Under `KnownSafeValues`, an unclassified
variable shows its name but not its value.

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

Any value that spans multiple lines or looks like a certificate or key (PEM
format) is redacted in every mode except `AllValues`.

`AllValues` is the deliberate opt-in for when you need the full picture and have
confirmed the output is safe to share. Treat a file produced under `AllValues`
as a secret: it can contain authentication headers, signed URLs, and API keys
embedded in endpoints. Prefer enabling it for one reproduction run rather than
leaving it on.

`Names` and `None` narrow the disclosure further when even variable names are
considered sensitive, at the cost of making the file much less useful for
diagnosis.

If a value you need is redacted under `KnownSafeValues` and it is not actually
sensitive, that is a classification gap worth reporting [in an
issue](https://github.com/open-telemetry/opentelemetry-dotnet/issues) so the
allowlist can be widened for everyone.

## Changing the configuration at runtime

The options are consumed through
[`IOptionsMonitor<SelfDiagnosticsOptions>`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.ioptionsmonitor-1),
so any configuration source that emits change notifications can reconfigure
self-diagnostics in a running process without rebuilding the provider or
restarting. That includes a file-based configuration provider registered with
`reloadOnChange: true` (the default for `appsettings.json` under the .NET host)
as well as remote configuration agents that push updates into an
`IConfiguration` source.

This is what makes it practical to turn diagnostics on during an incident: lower
`MinimumLevel` to `Debug`, get the output, and put it back, all without a
deployment.

To enable this, bind `SelfDiagnosticsOptions` to a configuration section (see
[Enable with code](#enable-with-code)):

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
> Environment variables and `Configure<SelfDiagnosticsOptions>`
> callbacks do **not** reload. Neither source emits change notifications, so a
> value from either is fixed for the lifetime of the process. Bind to a
> reloading configuration section if you want runtime control.
>
> If you cannot edit files on the target machine, you can still enable a sink by
> setting `OTEL_DOTNET_SELF_DIAGNOSTICS_SINKS` before the process starts, though
> that requires a restart to take effect.

## Legacy self-diagnostics mechanism

Before the mechanism described on this page was added, the SDK was configured by
dropping an `OTEL_DIAGNOSTICS.json` file into the working directory of the
process. The SDK re-read it periodically and wrote to a fixed-size file
named `ExecutableName.ProcessId.log`:

```json
{
    "LogDirectory": ".",
    "FileSize": 32768,
    "LogLevel": "Warning",
    "FormatMessage": "true"
}
```

* `LogDirectory` - absolute, or relative to the working directory.
* `FileSize` - the log file size in KiB, clamped to the range `[1024, 131072]`.
  The file is fixed-size and overwritten circularly, so older entries may be
  overwritten when the file is full.
* `LogLevel` - an
  [`EventLevel`](https://learn.microsoft.com/dotnet/api/system.diagnostics.tracing.eventlevel)
  name, not a `LogLevel` name.
* `FormatMessage` - whether `{0}`-style placeholders in messages are replaced
  with their argument values. Defaults to `false`.

Deleting the file disables it. An unparsable file is treated as invalid and no
output is produced.

That mechanism still works and is unchanged, but it is superseded by everything
described above and will be removed in a future major version. It runs
independently of the mechanism described above, so if you configure both you
will get two copies of every event, in two different formats.

> [!TIP]
> Prefer the new mechanism for anything new. The one remaining reason to
> use the legacy file is if the process is already running without a reloading
> configuration source and you need diagnostics without a restart.
