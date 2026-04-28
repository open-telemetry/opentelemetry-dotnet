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

// Wrap each exporter in a BatchLogRecordExportProcessor.
var defaultExportProcessor = new BatchLogRecordExportProcessor(otlpExporter1);
var paymentExportProcessor = new BatchLogRecordExportProcessor(otlpExporter2);

// Build the routing processor. Logs whose category name starts with
// "Payment." are sent to OTLP2; everything else goes to OTLP1.
var routingProcessor = new RoutingProcessor(
    categoryPrefix: "Payment.",
    defaultProcessor: defaultExportProcessor,
    paymentProcessor: paymentExportProcessor);

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
orderLogger.ProcessingOrder("ORD-001");

// --- Logs from "Payment.Processing" --> routed to OTLP2 ---
paymentLogger.ProcessingPayment("PAY-001");

// --- Another order log --> routed to OTLP1 ---
orderLogger.OrderCompleted("ORD-001");

// Dispose logger factory before the application ends.
// This will flush the remaining logs and shutdown the logging pipeline.
loggerFactory.Dispose();

internal static partial class LoggerExtensions
{
    [LoggerMessage(LogLevel.Information, "Processing order {OrderId}.")]
    public static partial void ProcessingOrder(this ILogger logger, string orderId);

    [LoggerMessage(LogLevel.Information, "Processing payment {PaymentId}.")]
    public static partial void ProcessingPayment(this ILogger logger, string paymentId);

    [LoggerMessage(LogLevel.Information, "Order {OrderId} completed.")]
    public static partial void OrderCompleted(this ILogger logger, string orderId);
}
