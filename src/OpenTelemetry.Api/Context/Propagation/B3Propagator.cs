// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Context.Propagation;

/// <summary>
/// A text map propagator for B3. See https://github.com/openzipkin/b3-propagation.
/// This class has been deprecated in favour of OpenTelemetry.Extensions.Propagators package.
/// </summary>
[Obsolete("Use B3Propagator class from OpenTelemetry.Extensions.Propagators namespace, shipped as part of OpenTelemetry.Extensions.Propagators package.")]
public sealed class B3Propagator : TextMapPropagator
{
    internal const string XB3TraceId = "X-B3-TraceId";
    internal const string XB3SpanId = "X-B3-SpanId";
    internal const string XB3ParentSpanId = "X-B3-ParentSpanId";
    internal const string XB3Sampled = "X-B3-Sampled";
    internal const string XB3Flags = "X-B3-Flags";
    internal const string XB3Combined = "b3";
    internal const char XB3CombinedDelimiter = '-';

    // Used as the upper ActivityTraceId.SIZE hex characters of the traceID. B3-propagation used to send
    // ActivityTraceId.SIZE hex characters (8-bytes traceId) in the past.
    internal const string UpperTraceId = "0000000000000000";

    // Sampled values via the X_B3_SAMPLED header.
    internal const char SampledValueChar = '1';
    internal const string SampledValue = "1";

    // Some old zipkin implementations may send true/false for the sampled header. Only use this for checking incoming values.
    internal const string LegacySampledValue = "true";

    // "Debug" sampled value.
    internal const string FlagsValue = "1";

    private static readonly HashSet<string> AllFields = [XB3TraceId, XB3SpanId, XB3ParentSpanId, XB3Sampled, XB3Flags];

    private static readonly HashSet<string> SampledValues = new(StringComparer.Ordinal) { SampledValue, LegacySampledValue };

    private readonly bool singleHeader;

    /// <summary>
    /// Initializes a new instance of the <see cref="B3Propagator"/> class.
    /// </summary>
    [Obsolete("Use B3Propagator class from OpenTelemetry.Extensions.Propagators namespace, shipped as part of OpenTelemetry.Extensions.Propagators package.")]
    public B3Propagator()
        : this(false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="B3Propagator"/> class.
    /// </summary>
    /// <param name="singleHeader">Determines whether to use single or multiple headers when extracting or injecting span context.</param>
    [Obsolete("Use B3Propagator class from OpenTelemetry.Extensions.Propagators namespace, shipped as part of OpenTelemetry.Extensions.Propagators package.")]
    public B3Propagator(bool singleHeader)
    {
        this.singleHeader = singleHeader;
    }

    /// <inheritdoc/>
    public override ISet<string> Fields => AllFields;

    /// <inheritdoc/>
    [Obsolete("Use B3Propagator class from OpenTelemetry.Extensions.Propagators namespace, shipped as part of OpenTelemetry.Extensions.Propagators package.")]
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
            OpenTelemetryApiEventSource.Log.FailedToExtractActivityContext(nameof(B3Propagator), "null carrier");
            return context;
        }

        if (getter == null)
        {
            OpenTelemetryApiEventSource.Log.FailedToExtractActivityContext(nameof(B3Propagator), "null getter");
            return context;
        }

        if (this.singleHeader)
        {
            return ExtractFromSingleHeader(context, carrier, getter);
        }
        else
        {
            return ExtractFromMultipleHeaders(context, carrier, getter);
        }
    }

    /// <inheritdoc/>
    [Obsolete("Use B3Propagator class from OpenTelemetry.Extensions.Propagators namespace, shipped as part of OpenTelemetry.Extensions.Propagators package.")]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
    public override void Inject<T>(PropagationContext context, T carrier, Action<T, string, string> setter)
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
    {
        if (context.ActivityContext.TraceId == default || context.ActivityContext.SpanId == default)
        {
            OpenTelemetryApiEventSource.Log.FailedToInjectActivityContext(nameof(B3Propagator), "invalid context");
            return;
        }

        if (carrier == null)
        {
            OpenTelemetryApiEventSource.Log.FailedToInjectActivityContext(nameof(B3Propagator), "null carrier");
            return;
        }

        if (setter == null)
        {
            OpenTelemetryApiEventSource.Log.FailedToInjectActivityContext(nameof(B3Propagator), "null setter");
            return;
        }

        if (this.singleHeader)
        {
            var sb = new StringBuilder();
            sb.Append(context.ActivityContext.TraceId.ToHexString());
            sb.Append(XB3CombinedDelimiter);
            sb.Append(context.ActivityContext.SpanId.ToHexString());
            if ((context.ActivityContext.TraceFlags & ActivityTraceFlags.Recorded) != 0)
            {
                sb.Append(XB3CombinedDelimiter);
                sb.Append(SampledValueChar);
            }

            setter(carrier, XB3Combined, sb.ToString());
        }
        else
        {
            setter(carrier, XB3TraceId, context.ActivityContext.TraceId.ToHexString());
            setter(carrier, XB3SpanId, context.ActivityContext.SpanId.ToHexString());
            if ((context.ActivityContext.TraceFlags & ActivityTraceFlags.Recorded) != 0)
            {
                setter(carrier, XB3Sampled, SampledValue);
            }
        }
    }

    private static PropagationContext ExtractFromMultipleHeaders<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>?> getter)
    {
        try
        {
            ActivityTraceId traceId;
            var traceIdStr = getter(carrier, XB3TraceId)?.FirstOrDefault();
            if (traceIdStr != null)
            {
                if (traceIdStr.Length == 16)
                {
                    // This is an 8-byte traceID.
                    traceIdStr = UpperTraceId + traceIdStr;
                }

                traceId = ActivityTraceId.CreateFromString(traceIdStr.AsSpan());
            }
            else
            {
                return context;
            }

            ActivitySpanId spanId;
            var spanIdStr = getter(carrier, XB3SpanId)?.FirstOrDefault();
            if (spanIdStr != null)
            {
                spanId = ActivitySpanId.CreateFromString(spanIdStr.AsSpan());
            }
            else
            {
                return context;
            }

            var traceOptions = ActivityTraceFlags.None;
            var xb3Sampled = getter(carrier, XB3Sampled)?.FirstOrDefault();
            if ((xb3Sampled != null && SampledValues.Contains(xb3Sampled))
                || FlagsValue.Equals(getter(carrier, XB3Flags)?.FirstOrDefault(), StringComparison.Ordinal))
            {
                traceOptions |= ActivityTraceFlags.Recorded;
            }

            return new PropagationContext(
                new ActivityContext(traceId, spanId, traceOptions, isRemote: true),
                context.Baggage);
        }
        catch (Exception e)
        {
            OpenTelemetryApiEventSource.Log.ActivityContextExtractException(nameof(B3Propagator), e);
            return context;
        }
    }

    private static PropagationContext ExtractFromSingleHeader<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>?> getter)
    {
        try
        {
            var header = getter(carrier, XB3Combined)?.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(header))
            {
                return context;
            }

            var parts = header!.Split(XB3CombinedDelimiter);
            if (parts.Length < 2 || parts.Length > 4)
            {
                return context;
            }

            var traceIdStr = parts[0];
            if (string.IsNullOrWhiteSpace(traceIdStr))
            {
                return context;
            }

            if (traceIdStr.Length == 16)
            {
                // This is an 8-byte traceID.
                traceIdStr = UpperTraceId + traceIdStr;
            }

            var traceId = ActivityTraceId.CreateFromString(traceIdStr.AsSpan());

            var spanIdStr = parts[1];
            if (string.IsNullOrWhiteSpace(spanIdStr))
            {
                return context;
            }

            var spanId = ActivitySpanId.CreateFromString(spanIdStr.AsSpan());

            var traceOptions = ActivityTraceFlags.None;
            if (parts.Length > 2)
            {
                var traceFlagsStr = parts[2];
                if (SampledValues.Contains(traceFlagsStr)
                    || FlagsValue.Equals(traceFlagsStr, StringComparison.Ordinal))
                {
                    traceOptions |= ActivityTraceFlags.Recorded;
                }
            }

            return new PropagationContext(
                new ActivityContext(traceId, spanId, traceOptions, isRemote: true),
                context.Baggage);
        }
        catch (Exception e)
        {
            OpenTelemetryApiEventSource.Log.ActivityContextExtractException(nameof(B3Propagator), e);
            return context;
        }
    }
}
