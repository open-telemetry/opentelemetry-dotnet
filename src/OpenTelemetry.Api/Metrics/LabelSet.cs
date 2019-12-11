// <copyright file="LabelSet.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Normalized name value pairs of metric labels.
    /// TODO: Most of the logic here would be moved into SDK from the API.
    /// </summary>
    public class LabelSet
    {
        /// <summary>
        /// Empty LabelSet.
        /// </summary>
        public static readonly LabelSet BlankLabelSet = new LabelSet();

        /// <summary>
        /// Initializes a new instance of the <see cref="LabelSet"/> class.
        /// </summary>
        /// <param name="labels">labels from which labelset should be constructed.</param>
        public LabelSet(IEnumerable<KeyValuePair<string, string>> labels)
        {
            this.Labels = labels;
            this.LabelSetEncoded = this.GetLabelSetEncoded(labels);
        }

        private LabelSet()
        {
        }

        /// <summary>
        /// Gets or sets the labels for this LabelSet.
        /// </summary>
        public IEnumerable<KeyValuePair<string, string>> Labels { get; set; }

        /// <summary>
        /// Gets or sets the label set in encoded form for this LabelSet.
        /// </summary>
        public string LabelSetEncoded { get; set; }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return this.LabelSetEncoded.GetHashCode();
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return this.LabelSetEncoded.Equals(((LabelSet)obj).LabelSetEncoded);
        }

        private string GetLabelSetEncoded(IEnumerable<KeyValuePair<string, string>> labels)
        {
            // Sort using keys.
            var ordered = this.Labels.OrderBy(x => x.Key);
            StringBuilder encoder = new StringBuilder();
            bool isFirstLabel = true;

            // simple encoding.
            foreach (KeyValuePair<string, string> label in ordered)
            {
                if (!isFirstLabel)
                {
                    encoder.Append("\0");
                }

                encoder.Append(label.Key);
                encoder.Append("\t");
                encoder.Append(label.Value);
                isFirstLabel = false;
            }

            return encoder.ToString();
        }
    }
}
