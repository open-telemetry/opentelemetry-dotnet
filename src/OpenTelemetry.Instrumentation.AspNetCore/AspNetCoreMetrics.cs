// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NET8_0_OR_GREATER
using OpenTelemetry.Instrumentation.AspNetCore.Implementation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.AspNetCore;

/// <summary>
/// Asp.Net Core Requests instrumentation.
/// </summary>
internal sealed class AspNetCoreMetrics : IDisposable
{
    private static readonly HashSet<string> DiagnosticSourceEvents = new()
    {
        "Microsoft.AspNetCore.Hosting.HttpRequestIn",
        "Microsoft.AspNetCore.Hosting.HttpRequestIn.Start",
        "Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop",
        "Microsoft.AspNetCore.Diagnostics.UnhandledException",
        "Microsoft.AspNetCore.Hosting.UnhandledException",
    };

    private readonly Func<string, object, object, bool> isEnabled = (eventName, _, _)
        => DiagnosticSourceEvents.Contains(eventName);

    private readonly DiagnosticSourceSubscriber diagnosticSourceSubscriber;

    internal AspNetCoreMetrics(RequestMethodHelper requestMethodHelper)
    {
        var metricsListener = new HttpInMetricsListener("Microsoft.AspNetCore", requestMethodHelper);
        this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(metricsListener, this.isEnabled, AspNetCoreInstrumentationEventSource.Log.UnknownErrorProcessingEvent);
        this.diagnosticSourceSubscriber.Subscribe();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.diagnosticSourceSubscriber?.Dispose();
    }
}
#endif
