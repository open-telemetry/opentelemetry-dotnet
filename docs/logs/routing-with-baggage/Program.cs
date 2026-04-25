// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;

// Create two OTLP exporters pointing at different destinations.
var otlpExporter1 = new OtlpLogExporter(new OtlpExporterOptions
{
    Endpoint = new Uri("http://localhost:4317"), // OTLP destination 1
});

var otlpExporter2 = new OtlpLogExporter(new OtlpExporterOptions
{
    Endpoint = new Uri("http://localhost:4318"), // OTLP destination 2
});

// Wrap each exporter in a SimpleLogRecordExportProcessor.
// (Use BatchLogRecordExportProcessor for production workloads.)
var processor1 = new SimpleLogRecordExportProcessor(otlpExporter1);
var processor2 = new SimpleLogRecordExportProcessor(otlpExporter2);

// Build the routing processor: logs emitted while Baggage
// contains "team=payments" go to OTLP2; everything else goes to OTLP1.
var routingProcessor = new BaggageRoutingProcessor(
    baggageKey: "team",
    baggageValueForSecondary: "payments",
    primaryProcessor: processor1,
    secondaryProcessor: processor2);

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(logging =>
    {
        logging.AddProcessor(routingProcessor);

        // Optional: also add a console exporter so you can see all logs locally.
        logging.AddConsoleExporter();
    });
});

var logger = loggerFactory.CreateLogger<Program>();

// --- Scenario 1: default baggage → routed to OTLP1 ---
Baggage.SetBaggage("team", "orders");
logger.LogInformation("Processing order {OrderId}.", "ORD-001");

// --- Scenario 2: "payments" baggage → routed to OTLP2 ---
Baggage.SetBaggage("team", "payments");
logger.LogInformation("Processing payment {PaymentId}.", "PAY-001");

// --- Scenario 3: back to a different team → routed to OTLP1 ---
Baggage.SetBaggage("team", "shipping");
logger.LogInformation("Shipping package {PackageId}.", "PKG-001");

// Dispose logger factory before the application ends.
// This will flush the remaining logs and shutdown the logging pipeline.
loggerFactory.Dispose();
