# Per-options-class coverage files

One file per options class. Each file follows the three-section shape defined
in
[`../../configuration-test-coverage.md`](../../configuration-test-coverage.md)
and the master plan: Section 1 inventory, Section 2 scenario grid, Section 3
recommendations.

## Status

Order of production follows the master plan's "largest surface / highest
guard value first" heuristic.

| # | Options class | File | Status |
| --- | --- | --- | --- |
| 1 | `OtlpExporterOptions` | [otlp-exporter-options.md](otlp-exporter-options.md) | done |
| 2 | `SdkLimitOptions` | [sdk-limit-options.md](sdk-limit-options.md) | done |
| 3 | `BatchExportActivityProcessorOptions` | [batch-export-activity-processor-options.md](batch-export-activity-processor-options.md) | done |
| 4 | `BatchExportLogRecordProcessorOptions` | [batch-export-logrecord-processor-options.md](batch-export-logrecord-processor-options.md) | done |
| 5 | `PeriodicExportingMetricReaderOptions` | [periodic-exporting-metric-reader-options.md](periodic-exporting-metric-reader-options.md) | done |
| 6 | `OpenTelemetryLoggerOptions` | [opentelemetry-logger-options.md](opentelemetry-logger-options.md) | done |
| 7 | `OtlpMtlsOptions` | [otlp-mtls-options.md](otlp-mtls-options.md) | done |
| 8 | `OtlpTlsOptions` | [otlp-tls-options.md](otlp-tls-options.md) | done |
| 9 | `ExperimentalOptions` | [experimental-options.md](experimental-options.md) | done |
| 10 | `OtlpExporterBuilderOptions` | [otlp-exporter-builder-options.md](otlp-exporter-builder-options.md) | done |
| 11 | `BatchExportProcessorOptions<T>` | [batch-export-processor-options.md](batch-export-processor-options.md) | done |
| 12 | `MetricReaderOptions` | [metric-reader-options.md](metric-reader-options.md) | done |
| 13 | `LogRecordExportProcessorOptions` | [log-record-export-processor-options.md](log-record-export-processor-options.md) | done |
| 14 | `ActivityExportProcessorOptions` | [activity-export-processor-options.md](activity-export-processor-options.md) | done |

## Conventions

- Section 1 is pure inventory from
  [`../existing-tests.md`](../existing-tests.md) - no opinion, no gap
  marking.
- Section 2 marks each scenario **covered** / **partial** / **missing**. If
  `existing-tests.md` does not mention a test for a behaviour, the row is
  **missing**, not **covered**.
- Section 3 sizes each recommendation as a reviewable PR unit (target test
  name, location, tier, observation mechanism, guarded issues, risks
  pinned, code-comment hint, risk vs reward).
- Guards-issues footer in each file must match the "Baseline tests
  required" lines on the corresponding issues in
  [`../../configuration-proposed-issues.md`](../../configuration-proposed-issues.md).

No test code is written in this cycle. Files here are maintainer-facing
planning artefacts only.
