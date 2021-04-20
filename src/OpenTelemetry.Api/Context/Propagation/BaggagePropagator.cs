// <copyright file="BaggagePropagator.cs" company="OpenTelemetry Authors">
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
    /// A text map propagator for W3C Baggage. See https://w3c.github.io/baggage/.
    /// </summary>
    public class BaggagePropagator : TextMapPropagator
    {
        internal const string BaggageHeaderName = "baggage";

        private const int MaxBaggageLength = 8192;
        private const int MaxBaggageItems = 180;

        /// <inheritdoc/>
        public override ISet<string> Fields => new HashSet<string> { BaggageHeaderName };

        /// <inheritdoc/>
        public override PropagationContext Extract<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>> getter)
        {
            if (context.Baggage != default)
            {
                // If baggage has already been extracted, perform a noop.
                return context;
            }

            if (carrier == null)
            {
                OpenTelemetryApiEventSource.Log.FailedToExtractBaggage(nameof(BaggagePropagator), "null carrier");
                return context;
            }

            if (getter == null)
            {
                OpenTelemetryApiEventSource.Log.FailedToExtractBaggage(nameof(BaggagePropagator), "null getter");
                return context;
            }

            try
            {
                Dictionary<string, string> baggage = null;
                var baggageCollection = getter(carrier, BaggageHeaderName);
                if (baggageCollection?.Any() ?? false)
                {
                    TryExtractBaggage(baggageCollection.ToArray(), out baggage);
                }

                return new PropagationContext(
                    context.ActivityContext,
                    baggage == null ? context.Baggage : new Baggage(baggage));
            }
            catch (Exception ex)
            {
                OpenTelemetryApiEventSource.Log.BaggageExtractException(nameof(BaggagePropagator), ex);
            }

            return context;
        }

        /// <inheritdoc/>
        public override void Inject<T>(PropagationContext context, T carrier, Action<T, string, string> setter)
        {
            if (carrier == null)
            {
                OpenTelemetryApiEventSource.Log.FailedToInjectBaggage(nameof(BaggagePropagator), "null carrier");
                return;
            }

            if (setter == null)
            {
                OpenTelemetryApiEventSource.Log.FailedToInjectBaggage(nameof(BaggagePropagator), "null setter");
                return;
            }

            using var e = context.Baggage.GetEnumerator();

            if (e.MoveNext() == true)
            {
                int itemCount = 0;
                StringBuilder baggage = new StringBuilder();
                do
                {
                    KeyValuePair<string, string> item = e.Current;
                    if (string.IsNullOrEmpty(item.Value))
                    {
                        continue;
                    }

                    baggage.Append(WebUtility.UrlEncode(item.Key)).Append('=').Append(WebUtility.UrlEncode(item.Value)).Append(',');
                }
                while (e.MoveNext() && ++itemCount < MaxBaggageItems && baggage.Length < MaxBaggageLength);
                baggage.Remove(baggage.Length - 1, 1);
                setter(carrier, BaggageHeaderName, baggage.ToString());
            }
        }

        internal static bool TryExtractBaggage(string[] baggageCollection, out Dictionary<string, string> baggage)
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
                        if (string.IsNullOrEmpty(baggageItem.Name) || string.IsNullOrEmpty(baggageItem.Value))
                        {
                            continue;
                        }

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
