// <copyright file="LabelSetSDK.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// LabelSet implementation.
    /// </summary>
    internal class LabelSetSDK : LabelSet
    {
        // encoded value is used internally by the SDK as a key in Dictionary.
        // This could potentially be made public, and combined with an
        // option to override Encoder, can avoid reencoding of labels
        // at Exporter level.
        private string labelSetEncoded;

        /// <summary>
        /// Initializes a new instance of the <see cref="LabelSet"/> class.
        /// </summary>
        /// <param name="labels">labels from which labelset should be constructed.</param>
        internal LabelSetSDK(IEnumerable<KeyValuePair<string, string>> labels)
        {
            this.Labels = this.SortAndDedup(labels);
            this.labelSetEncoded = this.GetLabelSetEncoded(this.Labels);
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
            return this.labelSetEncoded.Equals(((LabelSetSDK)obj).labelSetEncoded);
        }

        private IEnumerable<KeyValuePair<string, string>> SortAndDedup(IEnumerable<KeyValuePair<string, string>> labels)
        {
            // TODO - could be optimized to avoid creating List twice.
            var orderedList = labels.OrderBy(x => x.Key).ToList<KeyValuePair<string, string>>();
            if (orderedList.Count == 1)
            {
                return orderedList;
            }
            else
            {
                var dedupedList = new List<KeyValuePair<string, string>>();

                int dedupedListIndex = 0;
                dedupedList.Add(orderedList[dedupedListIndex]);
                for (int i = 1; i < orderedList.Count; i++)
                {                    
                    if (orderedList[i].Key.Equals(orderedList[i - 1].Key))
                    {
                        dedupedList[dedupedListIndex] = orderedList[i];
                    }
                    else
                    {
                        dedupedList.Add(orderedList[i]);
                        dedupedListIndex++;
                    }
                }

                return dedupedList;
            }
        }

        private string GetLabelSetEncoded(IEnumerable<KeyValuePair<string, string>> labels)
        {
            StringBuilder encoder = new StringBuilder();
            bool isFirstLabel = true;

            // simple encoding.
            foreach (var label in labels)
            {
                if (!isFirstLabel)
                {
                    encoder.Append(",");
                }

                encoder.Append(label.Key);
                encoder.Append("=");
                encoder.Append(label.Value);
                isFirstLabel = false;
            }

            return encoder.ToString();
        }
    }
}
