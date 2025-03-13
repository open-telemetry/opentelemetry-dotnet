// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Internal;
using OpenTracing.Propagation;

namespace OpenTelemetry.Shims.OpenTracing;

/// <summary>
/// Implements OpenTracing <see cref="global::OpenTracing.ITracer"/> interface
/// using OpenTelemetry <see cref="Trace.Tracer"/> implementation.
/// </summary>
public class TracerShim : global::OpenTracing.ITracer
{
    private readonly Trace.Tracer tracer;
    private readonly TextMapPropagator? definedPropagator;

    /// <summary>
    /// Initializes a new instance of the <see cref="TracerShim"/> class.
    /// </summary>
    /// <param name="tracerProvider"><see cref="Trace.TracerProvider"/>.</param>
    public TracerShim(Trace.TracerProvider tracerProvider)
        : this(tracerProvider, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TracerShim"/> class.
    /// </summary>
    /// <param name="tracerProvider"><see cref="Trace.TracerProvider"/>.</param>
    /// <param name="textFormat"><see cref="TextMapPropagator"/>.</param>
    public TracerShim(Trace.TracerProvider tracerProvider, TextMapPropagator? textFormat)
    {
        Guard.ThrowIfNull(tracerProvider);

        var assemblyName = typeof(TracerShim).Assembly.GetName();
        var version = assemblyName.Version;

        this.tracer = tracerProvider.GetTracer("opentracing-shim", version?.ToString());
        this.definedPropagator = textFormat;
        this.ScopeManager = new ScopeManagerShim();
    }

    /// <inheritdoc/>
    public global::OpenTracing.IScopeManager ScopeManager { get; }

    /// <inheritdoc/>
    public global::OpenTracing.ISpan? ActiveSpan => this.ScopeManager.Active?.Span;

    private TextMapPropagator Propagator => this.definedPropagator ?? Propagators.DefaultTextMapPropagator;

    /// <inheritdoc/>
    public global::OpenTracing.ISpanBuilder BuildSpan(string operationName)
    {
        return new SpanBuilderShim(this.tracer, operationName);
    }

    /// <inheritdoc/>
    public global::OpenTracing.ISpanContext? Extract<TCarrier>(IFormat<TCarrier> format, TCarrier carrier)
    {
        Guard.ThrowIfNull(format);
        Guard.ThrowIfNull(carrier);

        PropagationContext propagationContext = default;

        if ((format == BuiltinFormats.TextMap || format == BuiltinFormats.HttpHeaders) && carrier is ITextMap textMapCarrier)
        {
            var carrierMap = new Dictionary<string, IEnumerable<string>>();

            foreach (var entry in textMapCarrier)
            {
                carrierMap.Add(entry.Key, [entry.Value]);
            }

            static IEnumerable<string>? GetCarrierKeyValue(Dictionary<string, IEnumerable<string>> source, string key)
            {
                if (key == null || !source.TryGetValue(key, out var value))
                {
                    return null;
                }

                return value;
            }

            propagationContext = this.Propagator.Extract(propagationContext, carrierMap, GetCarrierKeyValue);
        }

        // TODO:
        //  Not sure what to do here. Really, Baggage should be returned and not set until this ISpanContext is turned into a live Span.
        //  But that code doesn't seem to exist.
        // Baggage.Current = propagationContext.Baggage;

        return !propagationContext.ActivityContext.IsValid() ? null : new SpanContextShim(new Trace.SpanContext(propagationContext.ActivityContext));
    }

    /// <inheritdoc/>
    public void Inject<TCarrier>(
        global::OpenTracing.ISpanContext spanContext,
        IFormat<TCarrier> format,
        TCarrier carrier)
    {
        Guard.ThrowIfNull(spanContext);
        var shim = Guard.ThrowIfNotOfType<SpanContextShim>(spanContext);
        Guard.ThrowIfNull(format);
        Guard.ThrowIfNull(carrier);

        if ((format == BuiltinFormats.TextMap || format == BuiltinFormats.HttpHeaders) && carrier is ITextMap textMapCarrier)
        {
            this.Propagator.Inject(
                new PropagationContext(shim.SpanContext, Baggage.Current),
                textMapCarrier,
                (instrumentation, key, value) => instrumentation.Set(key, value));
        }
    }
}
