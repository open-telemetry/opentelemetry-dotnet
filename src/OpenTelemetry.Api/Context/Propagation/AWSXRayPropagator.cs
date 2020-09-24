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
        private const int Version = 1;
        private const int RandomNumberHexDigits = 24;
        private const int EpochHexDigits = 8;
        private const int VersionDigits = 1;
        private const int ElementsCount = 3;
        private const int TotalLength = RandomNumberHexDigits + EpochHexDigits + VersionDigits + ElementsCount - 1;
        private const char TraceIdDelimiter = '-';

        private const string ParentKey = "Parent";
        private const int ParentIdHexDigits = 16;

        private const string SampledKey = "Sampled";
        private const char SampledValue = '1';
        private const char NotSampledValue = '0';

        private static readonly char[] ValidSeparators = { ';' };
        private static readonly HashSet<string> AllFields = new HashSet<string>() { AWSXRayTraceHeaderKey };

        /// <inheritdoc/>
        public ISet<string> Fields => AllFields;

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

                if (!TryParse(parentHeader, out var rootTraceId, out var parentId, out var sampleDecision))
                {
                    return context;
                }

                var rootId = ExtractRootTraceId(rootTraceId);
                var traceId = ActivityTraceId.CreateFromString(rootId.AsSpan());
                var spanId = ActivitySpanId.CreateFromString(parentId.AsSpan());
                var traceOptions = sampleDecision == SampledValue ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None;

                return new PropagationContext(new ActivityContext(traceId, spanId, traceOptions, isRemote: true), context.Baggage);
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

        internal static bool TryParse(string rawHeader, out string rootTraceId, out string parentId, out char sampleDecision)
        {
            rootTraceId = null;
            parentId = null;
            sampleDecision = default;

            try
            {
                if (string.IsNullOrEmpty(rawHeader))
                {
                    return false;
                }

                var keyValuePairs = rawHeader.Split(ValidSeparators, StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => value.Trim().Split(KeyValueDelimiter))
                    .ToDictionary(pair => pair[0], pair => pair[1]);

                if (!keyValuePairs.TryGetValue(RootKey, out var tmpValue))
                {
                    return false;
                }

                if (!IsRootTraceIdValid(tmpValue))
                {
                    return false;
                }

                rootTraceId = tmpValue;

                if (!keyValuePairs.TryGetValue(ParentKey, out tmpValue))
                {
                    return false;
                }

                if (!IsParentIdValid(tmpValue))
                {
                    return false;
                }

                parentId = tmpValue;

                if (!(keyValuePairs.TryGetValue(SampledKey, out tmpValue) && char.TryParse(tmpValue, out var tmpChar)))
                {
                    return false;
                }

                if (!IsSampleDecisionValid(tmpChar))
                {
                    return false;
                }

                sampleDecision = tmpChar;

                return true;
            }
            catch (IndexOutOfRangeException e)
            {
                OpenTelemetryApiEventSource.Log.ActivityContextExtractException(nameof(AWSXRayPropagator), e);
                return false;
            }
        }

        internal static string ExtractRootTraceId(string traceId)
        {
            string[] elements = traceId.Split(TraceIdDelimiter);
            return string.Concat(elements[1], elements[2]);
        }

        internal static string ToXRayTraceIdFormat(string traceId)
        {
            var timestamp = traceId.Substring(0, EpochHexDigits);
            var randomNumber = traceId.Substring(EpochHexDigits);

            var newTraceId = string.Concat(Version, TraceIdDelimiter, timestamp, TraceIdDelimiter, randomNumber);

            return newTraceId;
        }

        internal static bool IsRootTraceIdValid(string traceId)
        {
            if (string.IsNullOrWhiteSpace(traceId))
            {
                return false;
            }

            if (traceId.Length != TotalLength)
            {
                return false;
            }

            string[] elements = traceId.Split(TraceIdDelimiter);

            if (elements.Length != ElementsCount)
            {
                return false;
            }

            if (!int.TryParse(elements[0], out var idVersion))
            {
                return false;
            }

            if (idVersion != Version)
            {
                return false;
            }

            var idEpoch = elements[1];
            var idRand = elements[2];

            if (idEpoch.Length != EpochHexDigits || idRand.Length != RandomNumberHexDigits)
            {
                return false;
            }

            if (!int.TryParse(idEpoch, NumberStyles.HexNumber, null, out _))
            {
                return false;
            }

            if (!BigInteger.TryParse(idRand, NumberStyles.HexNumber, null, out _))
            {
                return false;
            }

            return true;
        }

        internal static bool IsParentIdValid(string id)
        {
            return id.Length == ParentIdHexDigits && long.TryParse(id, NumberStyles.HexNumber, null, out _);
        }

        internal static bool IsSampleDecisionValid(char sampleDecision)
        {
            return sampleDecision == SampledValue || sampleDecision == NotSampledValue;
        }
    }
}
