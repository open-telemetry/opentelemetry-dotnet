// <copyright file="NoopStatsTest.cs" company="OpenTelemetry Authors">
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
using System.Collections;
using System.Collections.Generic;
using OpenTelemetry.Stats.Measures;
using OpenTelemetry.Context;
using Xunit;

namespace OpenTelemetry.Stats.Test
{
    public class NoopStatsTest
    {
        private static readonly DistributedContextEntry TAG = new DistributedContextEntry("key", "value");
        private static readonly IMeasureDouble MEASURE =  MeasureDouble.Create("my measure", "description", "s");

        private readonly ITagContext tagContext = new TestTagContext();

        // The NoopStatsRecorder should do nothing, so this test just checks that record doesn't throw an
        // exception.
        [Fact]
        public void NoopStatsRecorder_Record()
        {
            NoopStats.NoopStatsRecorder.NewMeasureMap().Put(MEASURE, 5).Record(tagContext);
        }

        // The NoopStatsRecorder should do nothing, so this test just checks that record doesn't throw an
        // exception.
        [Fact]
        public void NoopStatsRecorder_RecordWithCurrentContext()
        {
            NoopStats.NoopStatsRecorder.NewMeasureMap().Put(MEASURE, 6).Record();
        }

        [Fact]
        public void NoopStatsRecorder_Record_DisallowNullTagContext()
        {
            var measureMap = NoopStats.NoopStatsRecorder.NewMeasureMap();
            Assert.Throws<ArgumentNullException>(() => measureMap.Record(null));
        }

        class TestTagContext : ITagContext
        {
            public IEnumerator<DistributedContextEntry> GetEnumerator()
            {
                var l =  new List<DistributedContextEntry>() { TAG };
                return l.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}
