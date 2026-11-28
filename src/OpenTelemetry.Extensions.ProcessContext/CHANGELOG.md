# Changelog

This file contains individual changes for the
OpenTelemetry.Extensions.ProcessContext package. For highlights and
announcements covering all components see: [Release Notes](../../RELEASENOTES.md).

## Unreleased

* Added Process Context Sharing ([OTEP
  4719](https://github.com/open-telemetry/opentelemetry-specification/blob/main/oteps/profiles/4719-process-ctx.md)):
  `AddProcessContext` extensions for `TracerProviderBuilder`,
  `MeterProviderBuilder` and `LoggerProviderBuilder`, `ProcessContextOptions`
  and `ProcessContextPublisher`, enabled with
  `OTEL_EXPERIMENTAL_PROCESS_CONTEXT_ENABLED`. Linux (x64, arm64) only.
  ([#8193](https://github.com/open-telemetry/opentelemetry-dotnet/pull/8193))
