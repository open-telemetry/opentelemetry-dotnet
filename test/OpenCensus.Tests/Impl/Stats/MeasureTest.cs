// <copyright file="MeasureTest.cs" company="OpenCensus Authors">
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
    using OpenCensus.Stats.Measures;
    using Xunit;

    public class MeasureTest
    {
        [Fact]
        public void TestConstants()
        {
            Assert.Equal(255, Measure.NameMaxLength);
        }

        [Fact]
        public void PreventTooLongMeasureName()
        {
            char[] chars = new char[Measure.NameMaxLength + 1];

            for (int i = 0; i < chars.Length; i++)
            {
                chars[i] = 'a';
            }

            String longName = new string(chars);
            Assert.Throws<ArgumentOutOfRangeException>(() => MeasureDouble.Create(longName, "description", "1"));
        }

        [Fact]
        public void PreventNonPrintableMeasureName()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MeasureDouble.Create("\u0002", "description", "1"));
        }

        [Fact]
        public void TestMeasureDoubleComponents()
        {
            IMeasure measurement = MeasureDouble.Create("Foo", "The description of Foo", "Mbit/s");
            Assert.Equal("Foo", measurement.Name);
            Assert.Equal("The description of Foo", measurement.Description);
            Assert.Equal("Mbit/s", measurement.Unit);
        }

        [Fact]
        public void Testmeasurelongcomponents()
        {
            IMeasure measurement = MeasureLong.Create("Bar", "The description of Bar", "1");
            Assert.Equal("Bar", measurement.Name);
            Assert.Equal("The description of Bar", measurement.Description);
            Assert.Equal("1", measurement.Unit);
        }

        [Fact]
        public void TestMeasureDoubleEquals()
        {
            Assert.Equal(MeasureDouble.Create("name", "description", "bit/s"), MeasureDouble.Create("name", "description", "bit/s"));
            Assert.NotEqual(MeasureDouble.Create("name", "description", "bit/s"), MeasureDouble.Create("name", "description 2", "bit/s"));
        }

        [Fact]
        public void TestMeasureLongEquals()
        {
            Assert.Equal(MeasureLong.Create("name", "description", "bit/s"), MeasureLong.Create("name", "description", "bit/s"));
            Assert.NotEqual(MeasureLong.Create("name", "description", "bit/s"), MeasureLong.Create("name", "description 2", "bit/s"));
        }

        [Fact]
        public void TestMatch()
        {
            List<IMeasure> measures =
                new List<IMeasure>() {
                    MeasureDouble.Create("measure1", "description", "1"),
                    MeasureLong.Create("measure2", "description", "1"),};
            List<String> outputs = new List<string>();
            foreach (IMeasure measure in measures)
            {
                outputs.Add(
                    measure.Match(
                        (arg) =>
                        {
                            return "double";
                        },
                        (arg) =>
                        {
                            return "long";
                        },
                        (arg) =>
                        {
                            throw new ArgumentException();
                        }));
            }

            Assert.Equal(new List<string>() { "double", "long" }, outputs);
        }

        [Fact]
        public void TestMeasureDoubleIsNotEqualToMeasureLong()
        {
            Assert.NotEqual(MeasureDouble.Create("name", "description", "bit/s"), (IMeasure)MeasureLong.Create("name", "description", "bit/s"));
        }
    }
}
