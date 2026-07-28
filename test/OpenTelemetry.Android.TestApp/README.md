# OpenTelemetry Android test app

`OpenTelemetry.Android.TestApp` is a headless `net10.0-android` application that
runs the OpenTelemetry SDK on an Android emulator and exercises logs, metrics
and traces with the real OTLP/HTTP exporter. It is the device half of the Android
end-to-end tests; the assertions are in `OpenTelemetry.Android.Tests`.

> [!NOTE]
> You must [install .NET for Android dependencies](https://aka.ms/dotnet-android-install-sdk)
> to build and run this project.

## How it works

- Tests run on the device via [Microsoft.Testing.Platform](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro)
  (`EnableMSTestRunner`), driven by the `dotnet new androidtest`
  instrumentation pattern in `TestInstrumentation.cs`.
- The app exports over OTLP/HTTP to `http://10.0.2.2:4318` - `10.0.2.2` is the
  emulator's alias for the host loopback.

## Running

Requires the `android` workload and a running emulator (API 24+).
