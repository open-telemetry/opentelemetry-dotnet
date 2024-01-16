# OpenTelemetry Stress Tests for Metrics

This stress test is specifically for Metrics SDK, and is based on the
[OpenTelemetry.Tests.Stress](../OpenTelemetry.Tests.Stress/README.md).

* [Running the stress test](#running-the-stress-test)

> [!NOTE]
> To run the stress tests for Histogram, comment out the `Run` method
for `Counter` and uncomment everything related to `Histogram` in the
[Program.cs](../OpenTelemetry.Tests.Stress.Metrics/Program.cs).

## Running the stress test

Open a console, run the following command from the current folder:

```sh
dotnet run --framework net8.0 --configuration Release
```
