// <copyright file="MeasureMapBuilderTest.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Stats.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using OpenCensus.Stats.Measurements;
    using OpenCensus.Stats.Measures;
    using OpenCensus.Utils;
    using Xunit;

    public class MeasureMapBuilderTest
    {

        private static readonly IMeasureDouble M1 = MakeSimpleMeasureDouble("m1");
        private static readonly IMeasureDouble M2 = MakeSimpleMeasureDouble("m2");
        private static readonly IMeasureLong M3 = MakeSimpleMeasureLong("m3");
        private static readonly IMeasureLong M4 = MakeSimpleMeasureLong("m4");

        [Fact]
        public void TestPutDouble()
        {
            var metrics = MeasureMapBuilder.Builder().Put(M1, 44.4).Build();
            AssertContains(metrics, MeasurementDouble.Create(M1, 44.4) );
        }

        [Fact]
        public void TestPutLong()
        {
            var metrics = MeasureMapBuilder.Builder().Put(M3, 9999L).Put(M4, 8888L).Build();
            AssertContains(metrics, MeasurementLong.Create(M3, 9999L), MeasurementLong.Create(M4, 8888L));
        }

        [Fact]
        public void TestCombination()
        {
            var metrics =
                MeasureMapBuilder.Builder()
                    .Put(M1, 44.4)
                    .Put(M2, 66.6)
                    .Put(M3, 9999L)
                    .Put(M4, 8888L)
                    .Build();
            AssertContains(
                metrics,
                MeasurementDouble.Create(M1, 44.4), MeasurementDouble.Create(M2, 66.6),
                MeasurementLong.Create(M3, 9999L),  MeasurementLong.Create(M4, 8888L));
        }

        [Fact]
        public void TestBuilderEmpty()
        {
            var metrics = MeasureMapBuilder.Builder().Build();
            AssertContains(metrics);
        }

        [Fact]
        public void TestBuilder()
        {
            var expected = new List<IMeasurement>(10);
            MeasureMapBuilder builder = MeasureMapBuilder.Builder();
            for (int i = 1; i <= 10; i++)
            {
                expected.Add(MeasurementDouble.Create(MakeSimpleMeasureDouble("m" + i), i * 11.1));
                builder.Put(MakeSimpleMeasureDouble("m" + i), i * 11.1);
                var expArray = expected.ToArray();
                AssertContains(builder.Build(), expArray);
            }
        }

        [Fact]
        public void TestDuplicateMeasureDoubles()
        {
            AssertContains(
                MeasureMapBuilder.Builder().Put(M1, 1.0).Put(M1, 2.0).Build(),
                MeasurementDouble.Create(M1, 2.0));
            AssertContains(
                MeasureMapBuilder.Builder().Put(M1, 1.0).Put(M1, 2.0).Put(M1, 3.0).Build(),
                MeasurementDouble.Create(M1, 3.0));
            AssertContains(
                MeasureMapBuilder.Builder().Put(M1, 1.0).Put(M2, 2.0).Put(M1, 3.0).Build(),
                MeasurementDouble.Create(M1, 3.0),
                MeasurementDouble.Create(M2, 2.0));
            AssertContains(
                MeasureMapBuilder.Builder().Put(M1, 1.0).Put(M1, 2.0).Put(M2, 2.0).Build(),
                MeasurementDouble.Create(M1, 2.0),
                MeasurementDouble.Create(M2, 2.0));
        }

        [Fact]
        public void TestDuplicateMeasureLongs()
        {
            AssertContains(
                MeasureMapBuilder.Builder().Put(M3, 100L).Put(M3, 100L).Build(),
                MeasurementLong.Create(M3, 100L));
            AssertContains(
                MeasureMapBuilder.Builder().Put(M3, 100L).Put(M3, 200L).Put(M3, 300L).Build(),
                MeasurementLong.Create(M3, 300L));
            AssertContains(
                MeasureMapBuilder.Builder().Put(M3, 100L).Put(M4, 200L).Put(M3, 300L).Build(),
                MeasurementLong.Create(M3, 300L),
                MeasurementLong.Create(M4, 200L));
            AssertContains(
                MeasureMapBuilder.Builder().Put(M3, 100L).Put(M3, 200L).Put(M4, 200L).Build(),
                MeasurementLong.Create(M3, 200L),
                MeasurementLong.Create(M4, 200L));
        }

        [Fact]
        public void TestDuplicateMeasures()
        {
            AssertContains(
                MeasureMapBuilder.Builder().Put(M3, 100L).Put(M1, 1.0).Put(M3, 300L).Build(),
                MeasurementLong.Create(M3, 300L),
                MeasurementDouble.Create(M1, 1.0));
            AssertContains(
                MeasureMapBuilder.Builder().Put(M2, 2.0).Put(M3, 100L).Put(M2, 3.0).Build(),
                MeasurementDouble.Create(M2, 3.0),
                MeasurementLong.Create(M3, 100L));
        }

        private static IMeasureDouble MakeSimpleMeasureDouble(String measure)
        {
            return MeasureDouble.Create(measure, measure + " description", "1");
        }

        private static IMeasureLong MakeSimpleMeasureLong(String measure)
        {
            return MeasureLong.Create(measure, measure + " description", "1");
        }

        private static void AssertContains(IEnumerable<IMeasurement> metrics, params IMeasurement[] measurements)
        {
            var expected = measurements.ToList();
            Assert.True(Collections.AreEquivalent(metrics, expected));
        }
    }
}
