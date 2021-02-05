// <copyright file="LabelSetSdk.cs" company="OpenTelemetry Authors">
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
using System.Text;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// LabelSet implementation.
    /// </summary>
    internal class LabelSetSdk : LabelSet
    {
        // encoded value is used internally by the SDK as a key in Dictionary.
        // This could potentially be made public, and combined with an
        // option to override Encoder, can avoid reencoding of labels
        // at Exporter level.
        private readonly string labelSetEncoded;

        /// <summary>
        /// Initializes a new instance of the <see cref="LabelSetSdk"/> class.
        /// </summary>
        /// <param name="labels">labels from which labelset should be constructed.</param>
        internal LabelSetSdk(IEnumerable<KeyValuePair<string, string>> labels)
        {
            this.Labels = SortAndDedup(labels);
            this.labelSetEncoded = GetLabelSetEncoded(this.Labels);
        }

        /// <summary>
        /// Gets or sets the sorted and de-duped labels for this LabelSet.
        /// For duplicated keys, the last value wins.
        /// </summary>
        public override IEnumerable<KeyValuePair<string, string>> Labels { get; set; }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return this.labelSetEncoded.GetHashCode();
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return this.labelSetEncoded.Equals(((LabelSetSdk)obj).labelSetEncoded, StringComparison.Ordinal);
        }

        private static IEnumerable<KeyValuePair<string, string>> SortAndDedup(IEnumerable<KeyValuePair<string, string>> labels)
        {
            var dedupedList = new SortedDictionary<string, KeyValuePair<string, string>>(StringComparer.Ordinal);
            foreach (var label in labels)
            {
                dedupedList[label.Key] = label;
            }

            return dedupedList.Values;
        }

        private static string GetLabelSetEncoded(IEnumerable<KeyValuePair<string, string>> labels)
        {
            StringBuilder encoder = new StringBuilder();
            bool isFirstLabel = true;

            // simple encoding.
            foreach (var label in labels)
            {
                if (!isFirstLabel)
                {
                    encoder.Append(',');
                }

                encoder.Append(label.Key);
                encoder.Append('=');
                encoder.Append(label.Value);
                isFirstLabel = false;
            }

            return encoder.ToString();
        }
    }
}
