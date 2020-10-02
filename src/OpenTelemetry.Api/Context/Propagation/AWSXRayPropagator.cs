// <copyright file="AWSXRayPropagator.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Context.Propagation
{
    /// <summary>
    /// Propagator for AWS X-Ray. See https://docs.aws.amazon.com/xray/latest/devguide/xray-concepts.html#xray-concepts-tracingheader.
    /// </summary>
    public class AWSXRayPropagator : IPropagator
    {
        private const string AWSXRayTraceHeaderKey = "X-Amzn-Trace-Id";
        private const char KeyValueDelimiter = '=';
        private const char TraceHeaderDelimiter = ';';

        private const string RootKey = "Root";
        private const string Version = "1";
        private const int RandomNumberHexDigits = 24;
        private const int EpochHexDigits = 8;
        private const int TotalLength = 35;
        private const char TraceIdDelimiter = '-';
        private const int TraceIdDelimiterFirstIndex = 1;
        private const int TraceIdDelimiterSecondIndex = 10;

        private const string ParentKey = "Parent";
        private const int ParentIdHexDigits = 16;

        private const string SampledKey = "Sampled";
        private const char SampledValue = '1';
        private const char NotSampledValue = '0';

        /// <inheritdoc/>
        public ISet<string> Fields => new HashSet<string>() { AWSXRayTraceHeaderKey };

        /// <inheritdoc/>
        public PropagationContext Extract<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>> getter)
        {
            if (context.ActivityContext.IsValid())
            {
                return context;
            }

            if (carrier == null)
            {
                OpenTelemetryApiEventSource.Log.FailedToExtractActivityContext(nameof(AWSXRayPropagator), "null carrier");
                return context;
            }

            if (getter == null)
            {
                OpenTelemetryApiEventSource.Log.FailedToExtractActivityContext(nameof(AWSXRayPropagator), "null getter");
                return context;
            }

            try
            {
                var parentTraceHeader = getter(carrier, AWSXRayTraceHeaderKey);

                if (parentTraceHeader == null || parentTraceHeader.Count() != 1)
                {
                    return context;
                }

                var parentHeader = parentTraceHeader.First();

                if (!TryParseXRayTraceHeader(parentHeader, out var newActivityContext))
                {
                    return context;
                }

                return new PropagationContext(newActivityContext, context.Baggage);
            }
            catch (Exception ex)
            {
                OpenTelemetryApiEventSource.Log.ActivityContextExtractException(nameof(AWSXRayPropagator), ex);
            }

            return context;
        }

        /// <inheritdoc/>
        public void Inject<T>(PropagationContext context, T carrier, Action<T, string, string> setter)
        {
            if (context.ActivityContext.TraceId == default || context.ActivityContext.SpanId == default)
            {
                OpenTelemetryApiEventSource.Log.FailedToInjectActivityContext(nameof(AWSXRayPropagator), "Invalid context");
                return;
            }

            if (carrier == null)
            {
                OpenTelemetryApiEventSource.Log.FailedToInjectActivityContext(nameof(AWSXRayPropagator), "null carrier");
                return;
            }

            if (setter == null)
            {
                OpenTelemetryApiEventSource.Log.FailedToInjectActivityContext(nameof(AWSXRayPropagator), "null setter");
                return;
            }

            var sb = new StringBuilder();
            sb.Append(RootKey);
            sb.Append(KeyValueDelimiter);
            sb.Append(ToXRayTraceIdFormat(context.ActivityContext.TraceId.ToHexString()));
            sb.Append(TraceHeaderDelimiter);
            sb.Append(ParentKey);
            sb.Append(KeyValueDelimiter);
            sb.Append(context.ActivityContext.SpanId.ToHexString());
            sb.Append(TraceHeaderDelimiter);
            sb.Append(SampledKey);
            sb.Append(KeyValueDelimiter);
            sb.Append((context.ActivityContext.TraceFlags & ActivityTraceFlags.Recorded) != 0 ? SampledValue : NotSampledValue);

            setter(carrier, AWSXRayTraceHeaderKey, sb.ToString());
        }

        internal static bool TryParseXRayTraceHeader(string rawHeader, out ActivityContext activityContext)
        {
            // from https://docs.aws.amazon.com/xray/latest/devguide/xray-concepts.html#xray-concepts-tracingheader
            // rawHeader format: Root=1-5759e988-bd862e3fe1be46a994272793;Parent=53995c3f42cd8ad8;Sampled=1

            activityContext = default;
            ReadOnlySpan<char> traceId = default;
            ReadOnlySpan<char> parentId = default;
            char traceOptions = default;

            if (string.IsNullOrEmpty(rawHeader))
            {
                return false;
            }

            ReadOnlySpan<char> header = rawHeader.AsSpan();
            while (header.Length > 0)
            {
                int delimiterIndex = header.IndexOf(TraceHeaderDelimiter);
                ReadOnlySpan<char> part;
                if (delimiterIndex >= 0)
                {
                    part = header.Slice(0, delimiterIndex);
                    header = header.Slice(delimiterIndex + 1);
                }
                else
                {
                    part = header.Slice(0);
                    header = header.Slice(header.Length);
                }

                ReadOnlySpan<char> trimmedPart = part.Trim();
                int equalsIndex = trimmedPart.IndexOf(KeyValueDelimiter);
                if (equalsIndex < 0)
                {
                    return false;
                }

                ReadOnlySpan<char> value = trimmedPart.Slice(equalsIndex + 1);
                if (trimmedPart.StartsWith(RootKey.AsSpan()))
                {
                    if (!TryParseOTFormatTraceId(value, out var otFormatTraceId))
                    {
                        return false;
                    }

                    traceId = otFormatTraceId;
                }
                else if (trimmedPart.StartsWith(ParentKey.AsSpan()))
                {
                    if (!IsParentIdValid(value))
                    {
                        return false;
                    }

                    parentId = value;
                }
                else if (trimmedPart.StartsWith(SampledKey.AsSpan()))
                {
                    if (!TryParseSampleDecision(value, out var sampleDecision))
                    {
                        return false;
                    }

                    traceOptions = sampleDecision;
                }
            }

            if (traceId == default || parentId == default || traceOptions == default)
            {
                return false;
            }

            var activityTraceId = ActivityTraceId.CreateFromString(traceId);
            var activityParentId = ActivitySpanId.CreateFromString(parentId);
            var activityTraceOptions = traceOptions == SampledValue ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None;

            activityContext = new ActivityContext(activityTraceId, activityParentId, activityTraceOptions, isRemote: true);

            return true;
        }

        internal static bool TryParseOTFormatTraceId(ReadOnlySpan<char> traceId, out ReadOnlySpan<char> otFormatTraceId)
        {
            otFormatTraceId = default;

            if (traceId.IsEmpty || traceId.IsWhiteSpace())
            {
                return false;
            }

            if (traceId.Length != TotalLength)
            {
                return false;
            }

            if (!traceId.StartsWith(Version.AsSpan()))
            {
                return false;
            }

            if (traceId[TraceIdDelimiterFirstIndex] != TraceIdDelimiter || traceId[TraceIdDelimiterSecondIndex] != TraceIdDelimiter)
            {
                return false;
            }

            var timestamp = traceId.Slice(TraceIdDelimiterFirstIndex + 1, EpochHexDigits);
            var randomNumber = traceId.Slice(TraceIdDelimiterSecondIndex + 1);
            if (timestamp.Length != EpochHexDigits || randomNumber.Length != RandomNumberHexDigits)
            {
                return false;
            }

            var timestampString = timestamp.ToString();
            var randomNumberString = randomNumber.ToString();
            if (!int.TryParse(timestampString, NumberStyles.HexNumber, null, out _))
            {
                return false;
            }

            if (!BigInteger.TryParse(randomNumberString, NumberStyles.HexNumber, null, out _))
            {
                return false;
            }

            otFormatTraceId = (timestampString + randomNumberString).AsSpan();

            return true;
        }

        internal static bool IsParentIdValid(ReadOnlySpan<char> parentId)
        {
            if (parentId.IsEmpty || parentId.IsWhiteSpace())
            {
                return false;
            }

            return parentId.Length == ParentIdHexDigits && long.TryParse(parentId.ToString(), NumberStyles.HexNumber, null, out _);
        }

        internal static bool TryParseSampleDecision(ReadOnlySpan<char> sampleDecision, out char result)
        {
            result = default;

            if (sampleDecision.IsEmpty || sampleDecision.IsWhiteSpace())
            {
                return false;
            }

            if (!char.TryParse(sampleDecision.ToString(), out var tempChar))
            {
                return false;
            }

            if (tempChar != SampledValue && tempChar != NotSampledValue)
            {
                return false;
            }

            result = tempChar;

            return true;
        }

        internal static string ToXRayTraceIdFormat(string traceId)
        {
            var sb = new StringBuilder();
            sb.Append(Version);
            sb.Append(TraceIdDelimiter);
            sb.Append(traceId.Substring(0, EpochHexDigits));
            sb.Append(TraceIdDelimiter);
            sb.Append(traceId.Substring(EpochHexDigits));

            return sb.ToString();
        }
    }
}
