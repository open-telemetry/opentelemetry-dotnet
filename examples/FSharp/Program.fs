// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

module Examples.AspNetCore.FSharp

open System
open System.Diagnostics.Metrics
open Examples.AspNetCore
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open OpenTelemetry.Instrumentation.AspNetCore
open OpenTelemetry.Logs
open OpenTelemetry.Metrics
open OpenTelemetry.Resources
open OpenTelemetry.Trace

[<EntryPoint>]
let main args =
    let instrumentationSource = new InstrumentationSource()
    let appBuilder = WebApplication.CreateBuilder(args)

    // Note: Switch between OTLP/Console by setting UseTracingExporter in appsettings.json.
    let tracingExporter = 
        appBuilder.Configuration.GetValue("UseTracingExporter", defaultValue = "CONSOLE")
        |> fun s -> s.ToUpperInvariant()

    // Note: Switch between Prometheus/OTLP/Console by setting UseMetricsExporter in appsettings.json.
    let metricsExporter = 
        appBuilder.Configuration.GetValue("UseMetricsExporter", defaultValue = "CONSOLE")
        |> fun s -> s.ToUpperInvariant()

    // Note: Switch between Console/OTLP by setting UseLogExporter in appsettings.json.
    let logExporter = 
        appBuilder.Configuration.GetValue("UseLogExporter", defaultValue = "CONSOLE")
        |> fun s -> s.ToUpperInvariant()

    // Note: Switch between Explicit/Exponential by setting HistogramAggregation in appsettings.json
    let histogramAggregation = 
        appBuilder.Configuration.GetValue("HistogramAggregation", defaultValue = "EXPLICIT")
        |> fun s -> s.ToUpperInvariant()

    // Create a service to expose ActivitySource, and Metric Instruments
    // for manual instrumentation
    appBuilder.Services.AddSingleton<InstrumentationSource>() |> ignore

    // Add HttpClient to the service provider for dependency injection.
    appBuilder.Services.AddHttpClient() |> ignore

    // Clear default logging providers used by WebApplication host.
    appBuilder.Logging.ClearProviders() |> ignore

    // Configure OpenTelemetry logging, metrics, & tracing with auto-start using the
    // AddOpenTelemetry extension from OpenTelemetry.Extensions.Hosting.
    appBuilder.Services.AddOpenTelemetry()
        .ConfigureResource(fun r ->
            r.AddService(
                serviceName = appBuilder.Configuration.GetValue("ServiceName", defaultValue = "otel-test"),
                serviceVersion = instrumentationSource.ActivitySource.Version,
                serviceInstanceId = Environment.MachineName)
            |> ignore)
        .WithTracing(fun builder ->
            // Tracing

            // Ensure the TracerProvider subscribes to any custom ActivitySources.
            builder
                .AddSource(instrumentationSource.ActivitySource.Name)
                .SetSampler(AlwaysOnSampler())
                .AddHttpClientInstrumentation()
                .AddAspNetCoreInstrumentation()
            |> ignore

            // Use IConfiguration binding for AspNetCore instrumentation options.
            appBuilder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(
                appBuilder.Configuration.GetSection("AspNetCoreInstrumentation"))
            |> ignore

            match tracingExporter with
            | "OTLP" ->
                builder.AddOtlpExporter(fun otlpOptions ->
                    // Use IConfiguration directly for Otlp exporter endpoint option.
                    otlpOptions.Endpoint <- 
                        Uri(appBuilder.Configuration.GetValue("Otlp:Endpoint", defaultValue = "http://localhost:4317")))
                |> ignore
            | _ ->
                builder.AddConsoleExporter() |> ignore)
        .WithMetrics(fun builder ->
            // Metrics

            // Ensure the MeterProvider subscribes to any custom Meters.
            builder
                .AddMeter(instrumentationSource.MeterName)
                .SetExemplarFilter(ExemplarFilterType.TraceBased)
                .AddRuntimeInstrumentation()
                .AddHttpClientInstrumentation()
                .AddAspNetCoreInstrumentation()
            |> ignore

            match histogramAggregation with
            | "EXPONENTIAL" ->
                builder.AddView(fun instrument ->
                    if instrument.GetType().GetGenericTypeDefinition() = typedefof<Histogram<_>> then
                        Base2ExponentialBucketHistogramConfiguration() :> MetricStreamConfiguration
                    else
                        null)
                |> ignore
            | _ ->
                // Explicit bounds histogram is the default.
                // No additional configuration necessary.
                ()

            match metricsExporter with
            | "PROMETHEUS" ->
                builder.AddPrometheusExporter() |> ignore
            | "OTLP" ->
                builder.AddOtlpExporter(fun otlpOptions ->
                    // Use IConfiguration directly for Otlp exporter endpoint option.
                    otlpOptions.Endpoint <- 
                        Uri(appBuilder.Configuration.GetValue("Otlp:Endpoint", defaultValue = "http://localhost:4317")))
                |> ignore
            | _ ->
                builder.AddConsoleExporter() |> ignore)
        .WithLogging(fun builder ->
            // Note: See appsettings.json Logging:OpenTelemetry section for configuration.

            match logExporter with
            | "OTLP" ->
                builder.AddOtlpExporter(fun otlpOptions ->
                    // Use IConfiguration directly for Otlp exporter endpoint option.
                    otlpOptions.Endpoint <- 
                        Uri(appBuilder.Configuration.GetValue("Otlp:Endpoint", defaultValue = "http://localhost:4317")))
                |> ignore
            | _ ->
                builder.AddConsoleExporter() |> ignore)
    |> ignore

    appBuilder.Services.AddControllers() |> ignore

    let app = appBuilder.Build()

    app.UseHttpsRedirection() |> ignore

    app.UseAuthorization() |> ignore

    app.MapControllers() |> ignore

    // Configure OpenTelemetry Prometheus AspNetCore middleware scrape endpoint if enabled.
    if metricsExporter.Equals("prometheus", StringComparison.OrdinalIgnoreCase) then
        app.UseOpenTelemetryPrometheusScrapingEndpoint() |> ignore

    app.Run()

    0
