# OpenTelemetry Android end-to-end tests

These tests validate that the OpenTelemetry SDK works end-to-end when running under
the Android runtime.

> [!NOTE]
> You must [install .NET for Android dependencies](https://aka.ms/dotnet-android-install-sdk)
> to build and run this project.

## What is covered

`OpenTelemetry.Android.TestApp` is a `net10.0-android` app that wires up the SDK
(logs, metrics and traces) with the real OTLP/HTTP exporter and emits each signal
while running on an Android emulator.

This host project:

1. Starts an in-process OTLP/HTTP receiver (`OtlpHttpCollector`) on the host,
   bound to port `4318`.
2. Installs the app on a connected emulator (`dotnet build -t:Install`) and runs
   it with `adb shell am instrument` (`AndroidAppFixture`). The app exports to
   `http://10.0.2.2:4318` - the emulator's alias for the host loopback - so the
   export is a real cross-process HTTP/protobuf call.
3. Asserts the receiver decoded the expected traces, metrics and logs, and that
   the on-device test run itself succeeded.

## Requirements

- The [`android` workload](https://learn.microsoft.com/dotnet/maui/android/):
  `dotnet workload install android`
- JDK 17.
- A **running Android emulator** (API level 24+). The tests do not boot one; the
  CI workflow uses [`reactivecircus/android-emulator-runner`](https://github.com/ReactiveCircus/android-emulator-runner)
  on a KVM-accelerated Ubuntu runner.

## Running

With an emulator already running:

```shell
dotnet test test/OpenTelemetry.Android.Tests/OpenTelemetry.Android.Tests.csproj
```
