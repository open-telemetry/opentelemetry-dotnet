# OpenTelemetry Apple test app

`OpenTelemetry.Apple.TestApp` is a headless `net10.0-ios` application that runs
the OpenTelemetry SDK on an iOS simulator and exercises logs, metrics and traces
with the real OTLP/HTTP exporter. It is the device half of the Apple end-to-end
tests; the assertions are in `OpenTelemetry.Apple.Tests`.

> [!NOTE]
> You must install [.NET for iOS](https://learn.microsoft.com/dotnet/maui/ios/)
> and Xcode to build and run this project, which requires macOS.

## How it works

- Tests run on the device via [Microsoft.Testing.Platform](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro)
  (`EnableMSTestRunner`), bootstrapped by the app's own entry point in
  `TestRunner.cs`.
- The app has no user interface. When the run finishes it writes a summary and a
  TRX report to its `Documents/TestResults` directory and then exits, so the host
  test project can read the outcome back out of the simulator.
- The app exports over OTLP/HTTP to `http://localhost:<port>`. The iOS simulator
  shares the host's network stack, so the collector running on the host is
  reachable over the loopback interface; the port is passed in through the
  `OTEL_TEST_OTLP_ENDPOINT` environment variable.
- `Info.plist` opts in to cleartext HTTP for local network destinations
  (`NSAllowsLocalNetworking`), which App Transport Security blocks by default.

## Running

Requires macOS, the `ios` workload and an iOS simulator (iOS 15+).
