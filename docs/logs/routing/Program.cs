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

// Build the routing processor. Logs whose category name starts with
// "Payment." are sent to OTLP2; everything else goes to OTLP1.
var routingProcessor = new RoutingProcessor(
    categoryPrefix: "Payment.",
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

// Both loggers share the same ILoggerFactory / LoggerProvider pipeline.
var orderLogger = loggerFactory.CreateLogger("Order.Processing");
var paymentLogger = loggerFactory.CreateLogger("Payment.Processing");

// --- Logs from "Order.Processing" --> routed to OTLP1 ---
orderLogger.LogInformation("Processing order {OrderId}.", "ORD-001");

// --- Logs from "Payment.Processing" --> routed to OTLP2 ---
paymentLogger.LogInformation("Processing payment {PaymentId}.", "PAY-001");

// --- Another order log --> routed to OTLP1 ---
orderLogger.LogInformation("Order {OrderId} completed.", "ORD-001");

// Dispose logger factory before the application ends.
// This will flush the remaining logs and shutdown the logging pipeline.
loggerFactory.Dispose();
