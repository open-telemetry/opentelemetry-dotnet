# Initializing the OpenTelemetry .NET SDK

There are three ways to initialize the SDK. Pick the one that matches your
application model.

| Entry Point | Best For | Multi-Signal | Lifecycle |
| --- | --- | --- | --- |
| [`services.AddOpenTelemetry()`][add] | ASP.NET Core, worker services, hosted apps | Yes | Managed by the host |
| [`OpenTelemetrySdk.Create()`][create] | Console apps, background jobs, CLI tools | Yes | Single `Dispose()` call |
| [`Sdk.CreateTracerProviderBuilder()`][tracer] | Legacy / <= 1.9.0 codebases, single-signal tests | No (per-signal) | Dispose each provider individually |

> [!TIP]
> For new projects, use `AddOpenTelemetry()` (hosted) or
> `OpenTelemetrySdk.Create()` (non-hosted). The per-signal `Sdk.Create*Builder`
> methods are retained for backward compatibility.

## Quick links

- [Host & DI-Integrated (`AddOpenTelemetry`)][add]
- [Unified Multi-Signal (`OpenTelemetrySdk.Create`)][create]
- [Per-Signal / Legacy (`Sdk.CreateTracerProviderBuilder`)][tracer]
- [Configure*Provider Extensions (library authors & modular setup)](./configure-opentelemetry-provider.md)

[add]: ./add-opentelemetry.md
[create]: ./opentelemetry-sdk-create.md
[tracer]: ./sdk-create-tracer-provider-builder.md
