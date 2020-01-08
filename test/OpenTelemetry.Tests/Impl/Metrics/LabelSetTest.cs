// <copyright file="LabelSetTest.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Metrics;
using OpenTelemetry.Metrics.Configuration;
using OpenTelemetry.Metrics.Export;
using Xunit;

namespace OpenTelemetry.Metrics.Test
{
    public class LabelSetTest
    {
        [Fact]
        public void LabelSetRemovesDuplicates()
        {
            var labels = new List<KeyValuePair<string, string>>();
            labels.Add(new KeyValuePair<string, string>("dim1", "value1"));
            labels.Add(new KeyValuePair<string, string>("dim1", "value2"));
            labels.Add(new KeyValuePair<string, string>("dim3", "value3"));
            labels.Add(new KeyValuePair<string, string>("dim4", "value4"));

            var labelSet = new LabelSetSdk(labels);
            Assert.True(labelSet.Labels.ToList<KeyValuePair<string, string>>().Count == 3);
            Assert.Contains<KeyValuePair<string, string>>(labelSet.Labels, kv => kv.Key.Equals("dim1") && kv.Value.Equals("value2"));
            Assert.DoesNotContain<KeyValuePair<string, string>>(labelSet.Labels, kv => kv.Key.Equals("dim1") && kv.Value.Equals("value1"));
            Assert.Contains<KeyValuePair<string, string>>(labelSet.Labels, kv => kv.Key.Equals("dim3") && kv.Value.Equals("value3"));
            Assert.Contains<KeyValuePair<string, string>>(labelSet.Labels, kv => kv.Key.Equals("dim4") && kv.Value.Equals("value4"));
        }

        [Fact]
        public void LabelSetRemovesDuplicatesWhenOnlySingleDuplicatedLabelExist()
        {
            var labels = new List<KeyValuePair<string, string>>();
            labels.Add(new KeyValuePair<string, string>("dim1", "value1"));
            labels.Add(new KeyValuePair<string, string>("dim1", "value2"));
            labels.Add(new KeyValuePair<string, string>("dim1", "value3"));

            var labelSet = new LabelSetSdk(labels);
            Assert.True(labelSet.Labels.ToList<KeyValuePair<string, string>>().Count == 1);
            Assert.Contains(labelSet.Labels, kv => kv.Key.Equals("dim1") && kv.Value.Equals("value3"));
            Assert.DoesNotContain(labelSet.Labels, kv => kv.Key.Equals("dim1") && kv.Value.Equals("value1"));
            Assert.DoesNotContain(labelSet.Labels, kv => kv.Key.Equals("dim1") && kv.Value.Equals("value2"));
        }

        [Fact]
        public void LabelSetEncodingIsSameForSameLabelsInDifferentOrder()
        {
            var labels1 = new List<KeyValuePair<string, string>>();
            labels1.Add(new KeyValuePair<string, string>("dim1", "value1"));
            labels1.Add(new KeyValuePair<string, string>("dim2", "value2"));
            labels1.Add(new KeyValuePair<string, string>("dim3", "value3"));
            // Construct labelset some labels.
            var labelSet1 = new LabelSetSdk(labels1);

            var labels2 = new List<KeyValuePair<string, string>>();
            labels1.Add(new KeyValuePair<string, string>("dim3", "value3"));
            labels1.Add(new KeyValuePair<string, string>("dim2", "value2"));
            labels1.Add(new KeyValuePair<string, string>("dim1", "value1"));
            // Construct another labelset with same labels but in different order.
            var labelSet2 = new LabelSetSdk(labels1);

            var hashSet = new HashSet<LabelSetSdk>();

            hashSet.Add(labelSet1);
            hashSet.Add(labelSet2);

            // As LabelSet uses encoded string as key, the hashSet should contain a single entry.
            Assert.True(hashSet.Count == 1);
            Assert.Equal(labelSet1.GetHashCode(), labelSet2.GetHashCode());
            Assert.Equal(labelSet1, labelSet2);

        }
    }
}
