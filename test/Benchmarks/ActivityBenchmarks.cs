// <copyright file="ActivityBenchmarks.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BenchmarkDotNet.Attributes;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Benchmarks
{
    [MemoryDiagnoser]
    public class ActivityBenchmarks
    {
        private static readonly Activity EmptyActivity;
        private static readonly Activity Activity;

        static ActivityBenchmarks()
        {
            EmptyActivity = new Activity("EmptyActivity");

            Activity = new Activity("Activity");
            Activity.AddTag("Tag1", "Value1");
            Activity.AddTag("Tag2", 2);
            Activity.AddTag("Tag3", false);
        }

        [Benchmark]
        public void EnumerateEmptyTagObjects()
        {
            object value;
            foreach (KeyValuePair<string, object> tag in EmptyActivity.TagObjects)
            {
                if (tag.Key == "Tag3")
                {
                    value = tag.Value;
                    break;
                }
            }
        }

        [Benchmark]
        public void LinqEmptyTagObjects()
        {
            EmptyActivity.TagObjects.FirstOrDefault(i => i.Key == "Tag3");
        }

        [Benchmark]
        public void GetTagValueEmptyTagObjects()
        {
            EmptyActivity.GetTagValue("Tag3");
        }

        [Benchmark]
        public void EnumerateNonemptyTagObjects()
        {
            object value;
            foreach (KeyValuePair<string, object> tag in Activity.TagObjects)
            {
                if (tag.Key == "Tag3")
                {
                    value = tag.Value;
                    break;
                }
            }
        }

        [Benchmark]
        public void LinqNonemptyTagObjects()
        {
            Activity.TagObjects.FirstOrDefault(i => i.Key == "Tag3");
        }

        [Benchmark]
        public void GetTagValueNonemptyTagObjects()
        {
            Activity.GetTagValue("Tag3");
        }
    }
}
