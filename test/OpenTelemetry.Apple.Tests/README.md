# OpenTelemetry Apple end-to-end tests

These tests validate that the OpenTelemetry SDK works end-to-end when running
under the iOS runtime.

> [!NOTE]
> You must install [.NET for iOS](https://learn.microsoft.com/dotnet/maui/ios/)
> and Xcode to build and run this project, which requires macOS.

## What is covered

`OpenTelemetry.Apple.TestApp` is a `net10.0-ios` app that wires up the SDK (logs,
metrics and traces) with the real OTLP/HTTP exporter and emits each signal while
running on an iOS simulator.

This host project:

1. Starts an in-process OTLP/HTTP receiver (`OtlpHttpCollector`) on the host,
   bound to `localhost` on a free port.
2. Builds the app, installs it on a simulator (`xcrun simctl install`) and runs
   it (`xcrun simctl launch --console-pty`) in `AppleAppFixture`. The app exports
   to the collector over the host loopback - the simulator shares the host's
   network stack - so the export is a real cross-process HTTP/protobuf call.
3. Waits for the TRX report written on the device to appear in the app's data
   container (`xcrun simctl get_app_container`) rather than for the app to exit,
   reads it to check the on-device run passed, and copies the results to the host
   output so CI can upload them as artifacts.
4. Asserts the receiver decoded the expected traces, metrics and logs.

## Requirements

- macOS with Xcode (`xcrun simctl` must be on the `PATH`).
- The [`ios` workload](https://learn.microsoft.com/dotnet/maui/ios/):
  `dotnet workload install ios`
- An iOS simulator runtime (iOS 15+). Unlike the Android tests, a simulator does
  not need to be running: the fixture boots one on demand and shuts it down
  again afterwards if it started it.

## Running

```shell
dotnet test test/OpenTelemetry.Apple.Tests/OpenTelemetry.Apple.Tests.csproj
```
