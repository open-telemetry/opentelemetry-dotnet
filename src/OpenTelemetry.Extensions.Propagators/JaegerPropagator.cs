// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Extensions.Propagators;

/// <summary>
/// A text map propagator for Jaeger trace context. See https://www.jaegertracing.io/docs/next-release/client-libraries/#propagation-format.
/// </summary>
[Obsolete(ObsoleteMessage)]
public class JaegerPropagator : TextMapPropagator
{
    internal const string JaegerHeader = "uber-trace-id";
    internal const string JaegerDelimiter = ":";
    internal const string JaegerDelimiterEncoded = "%3A"; // while the spec defines the delimiter as a ':', some clients will url encode headers.
    internal const string SampledValue = "1";

    internal static readonly string[] JaegerDelimiters = [JaegerDelimiter, JaegerDelimiterEncoded];

    private const string ObsoleteMessage = "The Jaeger propagator is obsolete and will be removed in a future version. The Jaeger propagation format has been deprecated in favor of W3C Trace Context. Use TraceContextPropagator instead. See https://www.jaegertracing.io/sdk-migration/#propagation-format and https://github.com/open-telemetry/opentelemetry-specification/issues/4827 for more information.";

    private static readonly int TraceId128BitLength = "0af7651916cd43dd8448eb211c80319c".Length;
    private static readonly int SpanIdLength = "00f067aa0ba902b7".Length;

    /// <inheritdoc/>
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
    public override ISet<string> Fields => new HashSet<string> { JaegerHeader };
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member

    /// <inheritdoc/>
    [Obsolete(ObsoleteMessage)]
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

            return !jaegerHeaderParsed
                ? context
                : new PropagationContext(
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
    [Obsolete(ObsoleteMessage)]
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

        var headerValue = jaegerHeader;
        if (!TryExtractTraceParts(
                headerValue,
                out var traceIdStr,
                out var spanIdStr,
                out var traceFlagsStr))
        {
            return false;
        }

        if (traceIdStr.Length < TraceId128BitLength)
        {
            traceIdStr = traceIdStr.PadLeft(TraceId128BitLength, '0');
        }

        traceId = ActivityTraceId.CreateFromString(traceIdStr.AsSpan());

        if (spanIdStr.Length < SpanIdLength)
        {
            spanIdStr = spanIdStr.PadLeft(SpanIdLength, '0');
        }

        spanId = ActivitySpanId.CreateFromString(spanIdStr.AsSpan());

        if (SampledValue.Equals(traceFlagsStr, StringComparison.Ordinal))
        {
            traceOptions |= ActivityTraceFlags.Recorded;
        }

        return true;
    }

    private static bool TryExtractTraceParts(
        string jaegerHeader,
        out string traceId,
        out string spanId,
        out string traceFlags)
    {
        traceId = string.Empty;
        spanId = string.Empty;
        traceFlags = string.Empty;

        var position = 0;
        var componentCount = 0;

        while (position <= jaegerHeader.Length)
        {
            var component = ReadNextComponent(jaegerHeader, ref position);
            if (component.IsEmpty)
            {
                if (position >= jaegerHeader.Length)
                {
                    break;
                }

                continue;
            }

            switch (componentCount)
            {
                case 0:
                    traceId = component.ToString();
                    break;

                case 1:
                    spanId = component.ToString();
                    break;

                case 2:
                    break;

                case 3:
                    traceFlags = component.ToString();
                    break;

                default:
                    return false;
            }

            componentCount++;

            if (position >= jaegerHeader.Length)
            {
                break;
            }
        }

        return componentCount == 4;
    }

    private static ReadOnlySpan<char> ReadNextComponent(string header, ref int position)
    {
        var colonIndex = header.IndexOf(JaegerDelimiter, position, StringComparison.Ordinal);
        var encodedIndex = header.IndexOf(JaegerDelimiterEncoded, position, StringComparison.Ordinal);

        var nextIndex = -1;
        var delimiterLength = 0;

        if (colonIndex >= 0 && (encodedIndex < 0 || colonIndex < encodedIndex))
        {
            nextIndex = colonIndex;
            delimiterLength = JaegerDelimiter.Length;
        }
        else if (encodedIndex >= 0)
        {
            nextIndex = encodedIndex;
            delimiterLength = JaegerDelimiterEncoded.Length;
        }

        if (nextIndex < 0)
        {
            var result = header.AsSpan(position);
            position = header.Length;
            return result;
        }

        var component = header.AsSpan(position, nextIndex - position);
        position = nextIndex + delimiterLength;
        return component;
    }
}
