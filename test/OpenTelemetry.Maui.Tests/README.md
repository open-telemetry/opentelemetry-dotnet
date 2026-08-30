# OpenTelemetry MAUI end-to-end tests

These tests validate that the OpenTelemetry SDK works end-to-end when it is
wired up through a .NET MAUI application host running under the Android runtime.

> [!NOTE]
> You must install the [`maui-android` workload](https://learn.microsoft.com/dotnet/maui/get-started/installation)
> and the [.NET for Android dependencies](https://aka.ms/dotnet-android-install-sdk)
> to build and run this project.

## What is covered

`OpenTelemetry.Maui.TestApp` is a `net10.0-android` MAUI app that registers the
SDK (logs, metrics and traces) in its `MauiProgram` with the real OTLP/HTTP
exporter and emits each signal while running on an Android emulator.

Unlike the Android and Apple end-to-end tests, which build the providers
directly, the on-device tests here resolve them from the service provider MAUI
created for its application host. That covers the MAUI hosting path and the DI
wiring in `OpenTelemetry.Extensions.Hosting` in addition to the SDK.

This host project:

1. Starts an in-process OTLP/HTTP receiver (`OtlpHttpCollector`) on the host,
   bound to port `4318`.
2. Installs the app on a connected emulator (`dotnet build -t:Install`) and runs
   it with `adb shell am instrument` (`MauiAppFixture`). The app exports to
   `http://10.0.2.2:4318` - the emulator's alias for the host loopback - so the
   export is a real cross-process HTTP/protobuf call.
3. Asserts the receiver decoded the expected traces, metrics and logs, with the
   expected instrumentation scopes and `service.name`, and that the on-device
   test run itself succeeded.

## Requirements

- The [`maui-android` workload](https://learn.microsoft.com/dotnet/maui/get-started/installation):
  `dotnet workload install maui-android`
- JDK 17.
- A **running Android emulator** (API level 24+). The tests do not boot one; the
  CI workflow uses [`reactivecircus/android-emulator-runner`](https://github.com/ReactiveCircus/android-emulator-runner)
  on a KVM-accelerated Ubuntu runner.

## Running

With an emulator already running:

```shell
dotnet test test/OpenTelemetry.Maui.Tests/OpenTelemetry.Maui.Tests.csproj
```
