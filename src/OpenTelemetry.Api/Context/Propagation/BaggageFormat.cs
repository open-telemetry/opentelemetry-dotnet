﻿// <copyright file="BaggageFormat.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using System.Net;
using System.Text;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Context.Propagation
{
    /// <summary>
    /// W3C baggage: https://github.com/w3c/baggage/blob/master/baggage/HTTP_HEADER_FORMAT.md.
    /// </summary>
    public class BaggageFormat : ITextFormat
    {
        internal const string BaggageHeaderName = "Baggage";

        private const int MaxBaggageLength = 8192;
        private const int MaxBaggageItems = 180;

        /// <inheritdoc/>
        public ISet<string> Fields => new HashSet<string> { BaggageHeaderName };

        /// <inheritdoc/>
        public PropagationContext Extract<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>> getter)
        {
            if (carrier == null)
            {
                OpenTelemetryApiEventSource.Log.FailedToExtractBaggage(nameof(BaggageFormat), "null carrier");
                return context;
            }

            if (getter == null)
            {
                OpenTelemetryApiEventSource.Log.FailedToExtractBaggage(nameof(BaggageFormat), "null getter");
                return context;
            }

            try
            {
                IEnumerable<KeyValuePair<string, string>> baggage = null;
                var baggageCollection = getter(carrier, BaggageHeaderName);
                if (baggageCollection?.Any() ?? false)
                {
                    TryExtractBaggage(baggageCollection.ToArray(), out baggage);
                }

                return new PropagationContext(
                    context.ActivityContext,
                    baggage ?? context.ActivityBaggage);
            }
            catch (Exception ex)
            {
                OpenTelemetryApiEventSource.Log.BaggageExtractException(nameof(BaggageFormat), ex);
            }

            return context;
        }

        /// <inheritdoc/>
        public void Inject<T>(PropagationContext context, T carrier, Action<T, string, string> setter)
        {
            if (carrier == null)
            {
                OpenTelemetryApiEventSource.Log.FailedToInjectBaggage(nameof(BaggageFormat), "null carrier");
                return;
            }

            if (setter == null)
            {
                OpenTelemetryApiEventSource.Log.FailedToInjectBaggage(nameof(BaggageFormat), "null setter");
                return;
            }

            using IEnumerator<KeyValuePair<string, string>> e = context.ActivityBaggage?.GetEnumerator();

            if (e?.MoveNext() == true)
            {
                int itemCount = 1;
                StringBuilder baggage = new StringBuilder();
                do
                {
                    KeyValuePair<string, string> item = e.Current;
                    baggage.Append(WebUtility.UrlEncode(item.Key)).Append('=').Append(WebUtility.UrlEncode(item.Value)).Append(',');
                }
                while (e.MoveNext() && itemCount++ < MaxBaggageItems && baggage.Length < MaxBaggageLength);
                baggage.Remove(baggage.Length - 1, 1);
                setter(carrier, BaggageHeaderName, baggage.ToString());
            }
        }

        internal static bool TryExtractBaggage(string[] baggageCollection, out IEnumerable<KeyValuePair<string, string>> baggage)
        {
            int baggageLength = -1;
            bool done = false;
            Dictionary<string, string> baggageDictionary = null;

            foreach (var item in baggageCollection)
            {
                if (done)
                {
                    break;
                }

                if (string.IsNullOrEmpty(item))
                {
                    continue;
                }

                foreach (var pair in item.Split(','))
                {
                    baggageLength += pair.Length + 1; // pair and comma

                    if (baggageLength >= MaxBaggageLength || baggageDictionary?.Count >= MaxBaggageItems)
                    {
                        done = true;
                        break;
                    }

                    if (NameValueHeaderValue.TryParse(pair, out NameValueHeaderValue baggageItem))
                    {
                        if (baggageDictionary == null)
                        {
                            baggageDictionary = new Dictionary<string, string>();
                        }

                        baggageDictionary[baggageItem.Name] = baggageItem.Value;
                    }
                }
            }

            baggage = baggageDictionary;
            return baggageDictionary != null;
        }
    }
}
