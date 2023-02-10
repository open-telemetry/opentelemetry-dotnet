# Getting Started with OpenTelemetry .NET in 5 Minutes

## Run pre-requisites

This tutorial uses docker to deploy the pre-requisites:
OTel Collector - receives telemetry from the app, and forwards to prometheus/tempo
Prometheus - metric store
Tempo - traces store
Grafana - UI to show metrics, traces and exemplars.

Navigate to current directory and run the following:

```sh
docker-compose up -d
```

If the above step success, all dependencies would be spun up and ready now.
To test, navigate to Grafana running at: "http://localhost:3000/".

## Run test app

Navigate to [Example Asp.Net Core App](../../../examples/AspNetCore/) directory
and run the following command:

```sh
dotnet run
```

Open the application ("http://localhost:5000/weatherforecast") using any browser.
You may use the following powershell script to generate load.

```powershell
while($true)
{
    $s = Invoke-WebRequest http://localhost:5000/weatherforecast
    Start-Sleep -Milliseconds 500
}
```

## Use Exemplars to navigate from Metrics to Traces

The application sends metrics (with exemplars), and traces to the OTel
Collector, which exporter metrics and traces to Prometheus and Tempo
respectively.

Please wait 1-2 minute before continuing so that enough data is generated and
exported.

Open Grafana, select Explorer, and open Prometheus. Select a metric named
"http_exemplar_duration_bucket", and plot the chart. Keep hitting the
application to trigger requests and generate telemetry. Enable "Exemplar" Toggle
in the UI and refresh.

Select any Exemplar to see the data. At the end click the "Query with Tempo", to
open the corresponding Trace in Tempo.
