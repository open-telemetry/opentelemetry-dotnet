# Process Context Sharing for OpenTelemetry .NET

> [!WARNING]
> This is a prototype of an experimental feature (diagnostic
> [OTEL1006](../../docs/diagnostics/experimental-apis/OTEL1006.md)). The
> specification it implements is at Alpha status and not merged yet, and the
> package name and location are not final.

This package implements Process Context Sharing, specified in
[open-telemetry/opentelemetry-specification#5370](https://github.com/open-telemetry/opentelemetry-specification/pull/5370)
and based on [OTEP
4719](https://github.com/open-telemetry/opentelemetry-specification/blob/main/oteps/profiles/4719-process-ctx.md).
It publishes the SDK's resolved `Resource` in a small named memory mapping so
that readers outside the process, such as the [OpenTelemetry eBPF
Profiler](https://github.com/open-telemetry/opentelemetry-ebpf-profiler), can
attach the same `service.name`, `service.instance.id` and other resource
attributes to the telemetry they collect about the process.

This matters most for attributes the reader cannot work out for itself. A
`service.instance.id` generated at startup, or a `service.name` set in code
rather than through `OTEL_SERVICE_NAME`, is invisible to an external reader
unless the process publishes it.

## Supported platforms

Linux on x64 and arm64, on .NET 10 and later. Everywhere else (Windows, macOS,
32-bit processes, and the .NET Framework and .NET Standard builds) every API is
a silent no-op.

The native calls (`memfd_create`, `ftruncate`, `mmap`, `madvise`, `prctl`,
`clock_gettime`) are made from managed code through `[LibraryImport]`, so the
package is trim and Native AOT compatible and ships no native library.

## Usage

Add the extension to any provider and enable it with the
`OTEL_EXPERIMENTAL_PROCESS_CONTEXT_ENABLED` environment variable (or any other
`IConfiguration` source):

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .ConfigureResource(r => r.AddService("checkout", serviceInstanceId: instanceId))
    .AddProcessContext()
    .AddOtlpExporter()
    .Build();
```

```sh
OTEL_EXPERIMENTAL_PROCESS_CONTEXT_ENABLED=true dotnet run
```

With dependency injection:

```csharp
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("checkout"))
    .WithTracing(t => t.AddProcessContext())
    .WithMetrics(m => m.AddProcessContext());
```

It can also be enabled in code, which takes precedence over configuration:

```csharp
.AddProcessContext(o => o.Enabled = true)
```

| Option     | Default                                                        | Description                                      |
|------------|----------------------------------------------------------------|--------------------------------------------------|
| `Enabled`  | `OTEL_EXPERIMENTAL_PROCESS_CONTEXT_ENABLED`, otherwise `false` | Whether the context is published.                |
| `Resource` | `null` (the provider's resolved resource)                      | A resource to publish instead of the provider's. |

The context is published once the provider has resolved its resource, and is
dropped when the provider is disposed. There is only one process context per
process: when several providers publish, the most recently built one that is
still alive wins, and providers which share the same resource (the usual case
with `services.AddOpenTelemetry().ConfigureResource(...)`) publish once.

### Publishing without a provider

`ProcessContextPublisher` publishes, updates and drops the context directly. It
also accepts the additional (non-resource) attributes that the specification
allows next to the resource:

```csharp
ProcessContextPublisher.Publish(resource);
ProcessContextPublisher.Publish(resource, [new("some.key", "value")]);
ProcessContextPublisher.Unpublish();
```

Use either this or the provider extensions, not both: the most recent call wins.

## What is published

The implementation follows the specification's publication, update and fork
rules:

* A one page private mapping, backed by a memfd named `OTEL_CTX`
  (`MFD_CLOEXEC | MFD_ALLOW_SEALING | MFD_NOEXEC_SEAL`, retried without
  `MFD_NOEXEC_SEAL`), or an anonymous mapping when memfd is not allowed. It is
  marked `MADV_DONTFORK` and named `OTEL_CTX` with
  `prctl(PR_SET_VMA, PR_SET_VMA_ANON_NAME)`. Depending on what the kernel allows
  it shows in `/proc/<pid>/maps` as `[anon_shmem:OTEL_CTX]`, `[anon:OTEL_CTX]`
  or `/memfd:OTEL_CTX`.
* A 32 byte header at the start of the mapping: the signature `OTEL_CTX`,
  version `2`, the payload size, a `CLOCK_BOOTTIME` timestamp and a pointer to
  the payload. The timestamp is written last, after a full memory fence. During
  an update it is zero while the payload pointer and size change, and every
  update writes a strictly greater value.
* The payload, in a separate native allocation: a protobuf
  `opentelemetry.proto.processcontext.v1development.ProcessContext` message
  written by a small built-in encoder (no `Google.Protobuf` dependency).
  Payloads over 64 KiB are not published.

Publishing happens when a provider is built and when the published resource
changes. Nothing runs on the span, metric or log record paths.

You can check the result on a running process with the [reference dump
script](https://github.com/open-telemetry/sig-profiling/blob/main/process-context/c-and-cpp/otel_process_ctx_dump.sh):

```sh
grep OTEL_CTX /proc/<pid>/maps
sudo ./otel_process_ctx_dump.sh <pid>
```

## Failures and diagnostics

Publication never throws into the application. If it fails (for example
because of a seccomp profile, an old kernel, or a kernel without
`CONFIG_ANON_VMA_NAME` and no memfd), the first failure is logged as a warning
and later ones at verbose level, through the
`OpenTelemetry-Extensions-ProcessContext` EventSource. Use
[self-diagnostics](../OpenTelemetry/README.md#self-diagnostics) to collect it.

## Not included

[Thread context sharing (OTEP
4947)](https://github.com/open-telemetry/opentelemetry-specification/blob/main/oteps/profiles/4947-thread-ctx.md),
which publishes the active trace and span per thread, is not part of this
package. It needs an ELF thread-local variable exported from a native library,
which cannot be done from managed code.
