// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using System.Reflection;
using OpenTelemetry.Instrumentation.AspNetCore.Implementation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.AspNetCore;

/// <summary>
/// Asp.Net Core Requests instrumentation.
/// </summary>
internal sealed class AspNetCoreMetrics : IDisposable
{
    internal static readonly AssemblyName AssemblyName = typeof(HttpInListener).Assembly.GetName();
    internal static readonly string InstrumentationName = AssemblyName.Name;
    internal static readonly string InstrumentationVersion = AssemblyName.Version.ToString();

    private static readonly HashSet<string> DiagnosticSourceEvents = new()
    {
        "Microsoft.AspNetCore.Hosting.HttpRequestIn",
        "Microsoft.AspNetCore.Hosting.HttpRequestIn.Start",
        "Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop",
    };

    private readonly Func<string, object, object, bool> isEnabled = (eventName, _, _)
        => DiagnosticSourceEvents.Contains(eventName);

    private readonly DiagnosticSourceSubscriber diagnosticSourceSubscriber;
    private readonly Meter meter;

    internal AspNetCoreMetrics(AspNetCoreMetricsInstrumentationOptions options)
    {
        Guard.ThrowIfNull(options);
        this.meter = new Meter(InstrumentationName, InstrumentationVersion);
        var metricsListener = new HttpInMetricsListener("Microsoft.AspNetCore", this.meter, options);
        this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(metricsListener, this.isEnabled, AspNetCoreInstrumentationEventSource.Log.UnknownErrorProcessingEvent);
        this.diagnosticSourceSubscriber.Subscribe();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.diagnosticSourceSubscriber?.Dispose();
        this.meter?.Dispose();
    }
}
