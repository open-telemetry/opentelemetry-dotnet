# Initializing the OpenTelemetry .NET SDK

There are three ways to initialize the SDK. Pick the one that matches your
application model.

| Entry Point | Best For | Multi-Signal | Lifecycle |
|---|---|---|---|
| [`services.AddOpenTelemetry()`](./add-opentelemetry.md) | ASP.NET Core, worker services, hosted apps | ✅ | Managed by the host |
| [`OpenTelemetrySdk.Create()`](./opentelemetry-sdk-create.md) | Console apps, background jobs, CLI tools | ✅ | Single `Dispose()` call |
| [`Sdk.CreateTracerProviderBuilder()`](./sdk-create-tracer-provider-builder.md) | Legacy / ≤ 1.9.0 codebases, single-signal tests | ❌ per-signal | Dispose each provider individually |

> **Recommendation:** For new projects, use `AddOpenTelemetry()` (hosted) or
> `OpenTelemetrySdk.Create()` (non-hosted). The per-signal `Sdk.Create*Builder`
> methods are retained for backward compatibility.

## Quick links

- [Host & DI-Integrated (`AddOpenTelemetry`)](./add-opentelemetry.md)
- [Unified Multi-Signal (`OpenTelemetrySdk.Create`)](./opentelemetry-sdk-create.md)
- [Per-Signal / Legacy (`Sdk.CreateTracerProviderBuilder`)](./sdk-create-tracer-provider-builder.md)
- [Configure*Provider Extensions (library authors & modular setup)](./configure-opentelemetry-provider.md)
