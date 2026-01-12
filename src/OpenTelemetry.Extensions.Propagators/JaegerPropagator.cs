// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Extensions.Propagators;

/// <summary>
/// A text map propagator for Jaeger trace context. See https://www.jaegertracing.io/docs/next-release/client-libraries/#propagation-format.
/// </summary>
[Obsolete("The Jaeger propagator is obsolete and will be removed in a future version. The Jaeger propagation format has been deprecated in favor of W3C Trace Context. Use TraceContextPropagator instead. See https://www.jaegertracing.io/sdk-migration/#propagation-format and https://github.com/open-telemetry/opentelemetry-specification/issues/4827 for more information.")]
public class JaegerPropagator : TextMapPropagator
{
    internal const string JaegerHeader = "uber-trace-id";
    internal const string JaegerDelimiter = ":";
    internal const string JaegerDelimiterEncoded = "%3A"; // while the spec defines the delimiter as a ':', some clients will url encode headers.
    internal const string SampledValue = "1";

    internal static readonly string[] JaegerDelimiters = [JaegerDelimiter, JaegerDelimiterEncoded];

    private static readonly int TraceId128BitLength = "0af7651916cd43dd8448eb211c80319c".Length;
    private static readonly int SpanIdLength = "00f067aa0ba902b7".Length;

    /// <inheritdoc/>
    [Obsolete("The Jaeger propagator is obsolete and will be removed in a future version. The Jaeger propagation format has been deprecated in favor of W3C Trace Context. Use TraceContextPropagator instead. See https://www.jaegertracing.io/sdk-migration/#propagation-format and https://github.com/open-telemetry/opentelemetry-specification/issues/4827 for more information.")]
    public override ISet<string> Fields => new HashSet<string> { JaegerHeader };

    /// <inheritdoc/>
    [Obsolete("The Jaeger propagator is obsolete and will be removed in a future version. The Jaeger propagation format has been deprecated in favor of W3C Trace Context. Use TraceContextPropagator instead. See https://www.jaegertracing.io/sdk-migration/#propagation-format and https://github.com/open-telemetry/opentelemetry-specification/issues/4827 for more information.")]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
    public override PropagationContext Extract<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>?> getter)
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
    {
        if (context.ActivityContext.IsValid())
        {
            // If a valid context has already been extracted, perform a noop.
            return context;
        }

        if (carrier == null)
        {
            OpenTelemetryPropagatorsEventSource.Log.FailedToExtractActivityContext(nameof(JaegerPropagator), "null carrier");
            return context;
        }

        if (getter == null)
        {
            OpenTelemetryPropagatorsEventSource.Log.FailedToExtractActivityContext(nameof(JaegerPropagator), "null getter");
            return context;
        }

        try
        {
            var jaegerHeaderCollection = getter(carrier, JaegerHeader);
            if (jaegerHeaderCollection == null)
            {
                return context;
            }

            var jaegerHeader = jaegerHeaderCollection.First();

            if (string.IsNullOrWhiteSpace(jaegerHeader))
            {
                return context;
            }

            var jaegerHeaderParsed = TryExtractTraceContext(jaegerHeader, out var traceId, out var spanId, out var traceOptions);

            if (!jaegerHeaderParsed)
            {
                return context;
            }

            return new PropagationContext(
                new ActivityContext(traceId, spanId, traceOptions, isRemote: true),
                context.Baggage);
        }
        catch (Exception ex)
        {
            OpenTelemetryPropagatorsEventSource.Log.ActivityContextExtractException(nameof(JaegerPropagator), ex);
        }

        return context;
    }

    /// <inheritdoc/>
    [Obsolete("The Jaeger propagator is obsolete and will be removed in a future version. The Jaeger propagation format has been deprecated in favor of W3C Trace Context. Use TraceContextPropagator instead. See https://www.jaegertracing.io/sdk-migration/#propagation-format and https://github.com/open-telemetry/opentelemetry-specification/issues/4827 for more information.")]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
    public override void Inject<T>(PropagationContext context, T carrier, Action<T, string, string> setter)
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
    {
        // from https://www.jaegertracing.io/docs/next-release/client-libraries/#propagation-format
        // parent id is optional and deprecated, will not attempt to set it.
        // 128 bit uber-trace-id=e0ad975be108cd107990683f59cda9e6:e422f3fe664f6342:0:1
        const string defaultParentSpanId = "0";

        if (context.ActivityContext.TraceId == default || context.ActivityContext.SpanId == default)
        {
            OpenTelemetryPropagatorsEventSource.Log.FailedToInjectActivityContext(nameof(JaegerPropagator), "Invalid context");
            return;
        }

        if (carrier == null)
        {
            OpenTelemetryPropagatorsEventSource.Log.FailedToInjectActivityContext(nameof(JaegerPropagator), "null carrier");
            return;
        }

        if (setter == null)
        {
            OpenTelemetryPropagatorsEventSource.Log.FailedToInjectActivityContext(nameof(JaegerPropagator), "null setter");
            return;
        }

        var flags = (context.ActivityContext.TraceFlags & ActivityTraceFlags.Recorded) != 0 ? "1" : "0";

        var jaegerTrace = string.Join(
            JaegerDelimiter,
            context.ActivityContext.TraceId.ToHexString(),
            context.ActivityContext.SpanId.ToHexString(),
            defaultParentSpanId,
            flags);

        setter(carrier, JaegerHeader, jaegerTrace);
    }

    internal static bool TryExtractTraceContext(string jaegerHeader, out ActivityTraceId traceId, out ActivitySpanId spanId, out ActivityTraceFlags traceOptions)
    {
        // from https://www.jaegertracing.io/docs/next-release/client-libraries/#propagation-format
        // parent id is optional and deprecated. will not attempt to store it.
        // 128 bit uber-trace-id=e0ad975be108cd107990683f59cda9e6:e422f3fe664f6342:0:1
        // 128 bit with encoded delimiter uber-trace-id=e0ad975be108cd107990683f59cda9e6%3Ae422f3fe664f6342%3A0%3A1
        //  64 bit uber-trace-id=7990683f59cda9e6:e422f3fe664f6342:0:1
        //  64 bit with encoded delimiter uber-trace-id=7990683f59cda9e6%3Ae422f3fe664f6342%3A0%3A1

        traceId = default;
        spanId = default;
        traceOptions = ActivityTraceFlags.None;

        if (string.IsNullOrWhiteSpace(jaegerHeader))
        {
            return false;
        }

        var traceComponents = jaegerHeader.Split(JaegerDelimiters, StringSplitOptions.RemoveEmptyEntries);
        if (traceComponents.Length != 4)
        {
            return false;
        }

        var traceIdStr = traceComponents[0];
        if (traceIdStr.Length < TraceId128BitLength)
        {
            traceIdStr = traceIdStr.PadLeft(TraceId128BitLength, '0');
        }

        traceId = ActivityTraceId.CreateFromString(traceIdStr.AsSpan());

        var spanIdStr = traceComponents[1];
        if (spanIdStr.Length < SpanIdLength)
        {
            spanIdStr = spanIdStr.PadLeft(SpanIdLength, '0');
        }

        spanId = ActivitySpanId.CreateFromString(spanIdStr.AsSpan());

        var traceFlagsStr = traceComponents[3];
        if (SampledValue.Equals(traceFlagsStr, StringComparison.Ordinal))
        {
            traceOptions |= ActivityTraceFlags.Recorded;
        }

        return true;
    }
}
