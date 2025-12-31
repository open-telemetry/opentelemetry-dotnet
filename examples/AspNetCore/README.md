# OpenTelemetry ASP.NET Core Web API Example

This example uses the new WebApplication host that ships with .NET
and shows how to setup

1. OpenTelemetry logging
2. OpenTelemetry metrics
3. OpenTelemetry tracing

`ResourceBuilder` is associated with OpenTelemetry to associate the
service name, version and the machine on which this program is running.

The sample rate is set to emit all the traces using `AlwaysOnSampler`.
You can try out different samplers like `TraceIdRatioBasedSampler`.

## Running the basic example

The example by default writes telemetry to stdout.

```powershell
dotnet run --project examples/AspNetCore/Examples.AspNetCore.csproj
```

To call the web service, use a browser to request `https://localhost:5001/weatherforecast`

## Running Dependencies via Docker

To enable telemetry export via OTLP, update the `appsettings.json` file
to replace `"console"` with `"otlp"`. Launching the application will then
send telemetry data via OTLP.

Use the provided "docker-compose.yaml" file to spin up the
required dependencies, including:

- **OTel Collector** Accept telemetry and forwards them to Tempo, Prometheus
- **Prometheus** to store metrics
- **Grafana (UI)** UI to view metrics, traces. (Exemplars can be used to jump
  from metrics to traces)
- **Tempo** to store traces // TODO: Add a logging store also.

Once the Docker containers are running, you can access the **Grafana UI** at:
[http://localhost:3000/](http://localhost:3000/)

## References

- [ASP.NET Core](https://learn.microsoft.com/aspnet/core/introduction-to-aspnet-core)
- [Docker](http://docker.com)
- [Prometheus](http://prometheus.io/docs)
- [Tempo](https://github.com/grafana/tempo)
