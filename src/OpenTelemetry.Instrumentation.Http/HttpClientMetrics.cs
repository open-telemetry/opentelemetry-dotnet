// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using System.Reflection;
using OpenTelemetry.Instrumentation.Http.Implementation;

namespace OpenTelemetry.Instrumentation.Http;

/// <summary>
/// HttpClient instrumentation.
/// </summary>
internal sealed class HttpClientMetrics : IDisposable
{
    internal static readonly AssemblyName AssemblyName = typeof(HttpClientMetrics).Assembly.GetName();
    internal static readonly string InstrumentationName = AssemblyName.Name;
    internal static readonly string InstrumentationVersion = AssemblyName.Version.ToString();

    private static readonly HashSet<string> ExcludedDiagnosticSourceEvents = new()
    {
        "System.Net.Http.Request",
        "System.Net.Http.Response",
    };

    private readonly DiagnosticSourceSubscriber diagnosticSourceSubscriber;
    private readonly Meter meter;

    private readonly Func<string, object, object, bool> isEnabled = (activityName, obj1, obj2)
        => !ExcludedDiagnosticSourceEvents.Contains(activityName);

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpClientMetrics"/> class.
    /// </summary>
    /// <param name="options">HttpClient metric instrumentation options.</param>
    public HttpClientMetrics(HttpClientMetricInstrumentationOptions options)
    {
        this.meter = new Meter(InstrumentationName, InstrumentationVersion);
        this.diagnosticSourceSubscriber = new DiagnosticSourceSubscriber(new HttpHandlerMetricsDiagnosticListener("HttpHandlerDiagnosticListener", this.meter, options), this.isEnabled, HttpInstrumentationEventSource.Log.UnknownErrorProcessingEvent);
        this.diagnosticSourceSubscriber.Subscribe();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.diagnosticSourceSubscriber?.Dispose();
        this.meter?.Dispose();
    }
}
