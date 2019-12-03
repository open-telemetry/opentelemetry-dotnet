// <copyright file="SimpleLabelSetEncoder.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenTelemetry.Metrics.Export;

namespace OpenTelemetry.Metrics
{
    public class SimpleLabelSetEncoder
    {
        private readonly IDictionary<string, LabelSet> labelSets = new ConcurrentDictionary<string, LabelSet>();

        public LabelSet GetLabelSet(IEnumerable<KeyValuePair<string, string>> labels)
        {
            var encodedKey = this.Encode(labels);

            if (!this.labelSets.TryGetValue(encodedKey, out var labelSet))
            {
                labelSet = new LabelSet(labels);
                this.labelSets.Add(encodedKey, labelSet);
            }

            return labelSet;
        }

        private string Encode(IEnumerable<KeyValuePair<string, string>> labels)
        {
            // Sort using keys.
            var ordered = labels.OrderBy(x => x.Key);
            StringBuilder encoder = new StringBuilder();
            bool isFirstLabel = true;
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
