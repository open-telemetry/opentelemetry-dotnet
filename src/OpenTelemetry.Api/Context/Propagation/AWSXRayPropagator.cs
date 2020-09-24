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

            var traceId = ToXRayTraceIdFormat(context.ActivityContext.TraceId.ToHexString());
            var spanId = context.ActivityContext.SpanId.ToHexString();
            var sampleDecision = (context.ActivityContext.TraceFlags & ActivityTraceFlags.Recorded) != 0 ? SampledValue : NotSampledValue;

            var newTraceHeader = string.Concat(RootKey, KeyValueDelimiter, traceId, TraceHeaderDelimiter, ParentKey, KeyValueDelimiter, spanId, TraceHeaderDelimiter, SampledKey, KeyValueDelimiter, sampleDecision);

            setter(carrier, AWSXRayTraceHeaderKey, newTraceHeader);
        }

        internal static bool TryParseXRayTraceHeader(string rawHeader, out ActivityContext activityContext)
        {
            activityContext = default;
            string traceId = null;
            string parentId = null;
            char traceOptions = default;

            if (string.IsNullOrEmpty(rawHeader))
            {
                return false;
            }

            int position = 0;
            while (position < rawHeader.Length)
            {
                int delimiterIndex = rawHeader.IndexOf(TraceHeaderDelimiter, position);
                string part;

                if (delimiterIndex >= 0)
                {
                    part = rawHeader.Substring(position, delimiterIndex - position);
                    position = delimiterIndex + 1;
                }
                else
                {
                    part = rawHeader.Substring(position);
                    position = rawHeader.Length;
                }

                string trimmedPart = part.Trim();
                int equalsIndex = trimmedPart.IndexOf(KeyValueDelimiter);
                if (equalsIndex < 0)
                {
                    return false;
                }

                string value = trimmedPart.Substring(equalsIndex + 1);

                if (trimmedPart.StartsWith(RootKey))
                {
                    if (!TryParseOTFormatTraceId(value, out var otFormatTraceId))
                    {
                        return false;
                    }

                    traceId = otFormatTraceId;
                }
                else if (trimmedPart.StartsWith(ParentKey))
                {
                    if (!IsParentIdValid(value))
                    {
                        return false;
                    }

                    parentId = value;
                }
                else if (trimmedPart.StartsWith(SampledKey))
                {
                    if (!TryParseSampleDecision(value, out var sampleDecision))
                    {
                        return false;
                    }

                    traceOptions = sampleDecision;
                }
            }

            if (traceId == null || parentId == null || traceOptions == default)
            {
                return false;
            }

            var activityTraceId = ActivityTraceId.CreateFromString(traceId.AsSpan());
            var activityParentId = ActivitySpanId.CreateFromString(parentId.AsSpan());
            var activityTraceOptions = traceOptions == SampledValue ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None;

            activityContext = new ActivityContext(activityTraceId, activityParentId, activityTraceOptions, isRemote: true);

            return true;
        }

        internal static bool TryParseOTFormatTraceId(string traceId, out string otFormatTraceId)
        {
            otFormatTraceId = null;

            if (string.IsNullOrWhiteSpace(traceId))
            {
                return false;
            }

            if (traceId.Length != TotalLength)
            {
                return false;
            }

            if (!traceId.StartsWith(Version))
            {
                return false;
            }

            if (traceId[TraceIdDelimiterFirstIndex] != TraceIdDelimiter || traceId[TraceIdDelimiterSecondIndex] != TraceIdDelimiter)
            {
                return false;
            }

            var timestamp = traceId.Substring(TraceIdDelimiterFirstIndex + 1, EpochHexDigits);
            var randomNumber = traceId.Substring(TraceIdDelimiterSecondIndex + 1);

            if (timestamp.Length != EpochHexDigits || randomNumber.Length != RandomNumberHexDigits)
            {
                return false;
            }

            if (!int.TryParse(timestamp, NumberStyles.HexNumber, null, out _))
            {
                return false;
            }

            if (!BigInteger.TryParse(randomNumber, NumberStyles.HexNumber, null, out _))
            {
                return false;
            }

            otFormatTraceId = string.Concat(timestamp, randomNumber);

            return true;
        }

        internal static bool IsParentIdValid(string parentId)
        {
            if (string.IsNullOrWhiteSpace(parentId))
            {
                return false;
            }

            return parentId.Length == ParentIdHexDigits && long.TryParse(parentId, NumberStyles.HexNumber, null, out _);
        }

        internal static bool TryParseSampleDecision(string sampleDecision, out char result)
        {
            result = default;

            if (string.IsNullOrWhiteSpace(sampleDecision))
            {
                return false;
            }

            if (!char.TryParse(sampleDecision, out var tempChar))
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
            var timestamp = traceId.Substring(0, EpochHexDigits);
            var randomNumber = traceId.Substring(EpochHexDigits);

            var newTraceId = string.Concat(Version, TraceIdDelimiter, timestamp, TraceIdDelimiter, randomNumber);

            return newTraceId;
        }
    }
}
