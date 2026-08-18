# OpenTelemetry MAUI test app

`OpenTelemetry.Maui.TestApp` is a .NET MAUI application that runs the
OpenTelemetry SDK inside a MAUI application host on an Android emulator
and exercises logs, metrics and traces with the real OTLP/HTTP exporter. It is
the device half of the MAUI end-to-end tests; the assertions are in
`OpenTelemetry.Maui.Tests`.

> [!NOTE]
> You must install the [`maui-android` workload](https://learn.microsoft.com/dotnet/maui/get-started/installation)
> and the [.NET for Android dependencies](https://aka.ms/dotnet-android-install-sdk)
> to build and run this project.

## How it works

- `MauiProgram.CreateMauiApp()` is the code under test. It registers the SDK the
  way a MAUI app would: through the application host's service collection with
  `IServiceCollection.AddOpenTelemetry()` from `OpenTelemetry.Extensions.Hosting`
  for traces and metrics, and through `MauiAppBuilder.Logging` for logs.
- MAUI's own Android startup builds the host: `MainApplication` derives from
  `MauiApplication`, which calls `CreateMauiApp()` from `AttachBaseContext` as
  the process starts. The on-device tests then resolve the providers from
  `IPlatformApplication.Current.Services`, so a failure to wire the SDK up
  through MAUI is a test failure.
- A MAUI app is not an `IHost`, so it never runs the `IHostedService` that
  `AddOpenTelemetry()` registers to start the providers. They are created when
  they are first resolved instead, which is why the tests resolve `TracerProvider`
  and `MeterProvider` before emitting any telemetry.
- Log records are exported with `ExportProcessorType.Simple` because the
  `ILoggerFactory` is owned by the container, so the tests cannot dispose it to
  flush the pipeline the way they can call `ForceFlush()` on the other providers.
- Tests run on the device via
  [Microsoft.Testing.Platform](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro)
  (`EnableMSTestRunner`), driven by the `dotnet new androidtest` instrumentation
  pattern in `Platforms/Android/TestInstrumentation.cs`. The app declares no
  activity: nothing but the instrumentation drives it, so no window is created.
- The app exports over OTLP/HTTP to `http://10.0.2.2:4318` - `10.0.2.2` is the
  emulator's alias for the host loopback.

## Running

Requires the `maui-android` workload and a running emulator (API 24+).
