// <copyright file="BaggageFormat.cs" company="OpenTelemetry Authors">
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
        private const string Baggage = "baggage";
        private const int MaxBaggageLength = 1024;

        /// <inheritdoc/>
        public ISet<string> Fields => new HashSet<string> { Baggage };

        /// <inheritdoc/>
        public TextFormatContext Extract<T>(TextFormatContext context, T carrier, Func<T, string, IEnumerable<string>> getter)
        {
            if (carrier == null)
            {
                OpenTelemetryApiEventSource.Log.FailedToInjectActivityContext("null carrier");
                return context;
            }

            if (getter == null)
            {
                OpenTelemetryApiEventSource.Log.FailedToExtractContext("null getter");
                return context;
            }

            try
            {
                IEnumerable<KeyValuePair<string, string>> baggage = null;
                var baggageCollection = getter(carrier, Baggage);
                if (baggageCollection?.Any() ?? false)
                {
                    TryExtractTracestateBaggage(baggageCollection.ToArray(), out baggage);
                }

                return new TextFormatContext(
                    context.ActivityContext,
                    baggage ?? context.ActivityBaggage);
            }
            catch (Exception ex)
            {
                OpenTelemetryApiEventSource.Log.ActivityContextExtractException(ex);
            }

            return context;
        }

        /// <inheritdoc/>
        public void Inject<T>(TextFormatContext context, T carrier, Action<T, string, string> setter)
        {
            if (carrier == null)
            {
                OpenTelemetryApiEventSource.Log.FailedToInjectActivityContext("null carrier");
                return;
            }

            if (setter == null)
            {
                OpenTelemetryApiEventSource.Log.FailedToInjectActivityContext("null setter");
                return;
            }

            using IEnumerator<KeyValuePair<string, string>> e = context.ActivityBaggage?.GetEnumerator();

            if (e != null && e.MoveNext())
            {
                StringBuilder baggage = new StringBuilder();
                do
                {
                    KeyValuePair<string, string> item = e.Current;
                    baggage.Append(WebUtility.UrlEncode(item.Key)).Append('=').Append(WebUtility.UrlEncode(item.Value)).Append(',');
                }
                while (e.MoveNext());
                baggage.Remove(baggage.Length - 1, 1);
                setter(carrier, Baggage, baggage.ToString());
            }
        }

        internal static bool TryExtractTracestateBaggage(string[] baggageCollection, out IEnumerable<KeyValuePair<string, string>> baggage)
        {
            int baggageLength = -1;
            Dictionary<string, string> baggageDictionary = null;

            foreach (var item in baggageCollection)
            {
                if (baggageLength >= MaxBaggageLength)
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

                    if (baggageLength >= MaxBaggageLength)
                    {
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
