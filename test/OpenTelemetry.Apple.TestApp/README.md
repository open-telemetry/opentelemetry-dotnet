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
- The app has no user interface, and nothing pumps its main run loop, so it is
  not guaranteed to get any further than running the tests. The outcome is
  taken from the TRX report the test platform writes to the app's
  `Documents/TestResults` directory as the run finishes, which the host test
  project reads back out of the simulator. The summary the app writes afterwards
  is only used for diagnostics.
- The app exports over OTLP/HTTP to `http://localhost:<port>`. The iOS simulator
  shares the host's network stack, so the collector running on the host is
  reachable over the loopback interface; the port is passed in through the
  `OTEL_TEST_OTLP_ENDPOINT` environment variable.
- `Info.plist` opts in to cleartext HTTP for local network destinations
  (`NSAllowsLocalNetworking`), which App Transport Security blocks by default.

## Running

Requires macOS, the `ios` workload and an iOS simulator (iOS 15+).
