# OpenTelemetry Blazor WebAssembly end-to-end tests

These tests validate that the OpenTelemetry SDK works end-to-end in an ASP.NET
Core Blazor WebAssembly application running under the browser WASM runtime.

## What is covered

`OpenTelemetry.BlazorWasm.TestApp` is a Blazor WebAssembly app that wires up the
SDK (logs, metrics and traces) with the real OTLP/HTTP exporter and exercises
each signal, including HTTP client instrumentation.

The test:

1. Publishes the app and serves it, together with an in-process OTLP/HTTP
   receiver from the same origin.
2. Loads the app in headless Chromium using
   [Playwright](https://playwright.dev/dotnet/).
3. Asserts the receiver decoded the expected traces, metrics and logs.

## Required runtime feature switches

Blazor WebAssembly disables several runtime features by default to reduce the
download size. For OpenTelemetry to work these must be enabled in the
`OpenTelemetry.BlazorWasm.TestApp.csproj` project file:

```xml
<EventSourceSupport>true</EventSourceSupport>
<HttpActivityPropagationSupport>true</HttpActivityPropagationSupport>
<MetricsSupport>true</MetricsSupport>
```

## Running locally

The [`wasm-tools`](https://learn.microsoft.com/aspnet/core/blazor/tooling)
workload is required to publish the Blazor client:

```shell
dotnet workload install wasm-tools
```

Then run the tests. The Playwright browser is installed automatically by the test
fixture on first run:

```shell
dotnet test test/OpenTelemetry.BlazorWasm.Tests/OpenTelemetry.BlazorWasm.Tests.csproj
```

On failure a screenshot, a Playwright trace (`.zip`, viewable at
<https://trace.playwright.dev>) and a video are written to a `playwright`
directory under the test output.
