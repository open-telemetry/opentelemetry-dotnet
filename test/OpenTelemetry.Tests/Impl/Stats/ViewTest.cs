// <copyright file="ViewTest.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using OpenTelemetry.Stats.Aggregations;
using OpenTelemetry.Stats.Measures;
using OpenTelemetry.Tags;
using Xunit;

namespace OpenTelemetry.Stats.Test
{
    public class ViewTest
    {
        private static readonly IViewName Name = ViewName.Create("test-view-name");
        private static readonly string Description = "test-view-name description";
        private static readonly IMeasure Measure =
            MeasureDouble.Create("measure", "measure description", "1");

        private static readonly string Foo = "foo";
        private static readonly string Bar = "bar";
        private static readonly List<string> keys = new List<string>() { Foo, Bar };
        private static readonly IMean Mean = Aggregations.Mean.Create();
        private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan TwoMinutes = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan NegTenSeconds = TimeSpan.FromSeconds(-10);

        [Fact]
        public void TestConstants()
        {
            Assert.Equal(255, ViewName.NameMaxLength);
        }

        [Fact]
        public void TestDistributionView()
        {
            var view = View.Create(Name, Description, Measure, Mean, keys);
            Assert.Equal(Name, view.Name);
            Assert.Equal(Description, view.Description);
            Assert.Equal(Measure.Name, view.Measure.Name);
            Assert.Equal(Mean, view.Aggregation);
            Assert.Equal(2, view.Columns.Count);
            Assert.Equal(Foo, view.Columns[0]);
            Assert.Equal(Bar, view.Columns[1]);
        }

        [Fact]
        public void testViewEquals()
        {

            var view1 = View.Create(Name, Description, Measure, Mean, keys);
            var view2 = View.Create(Name, Description, Measure, Mean, keys);
            Assert.Equal(view1, view2);
            var view3 = View.Create(Name, Description + 2, Measure, Mean, keys);
            Assert.NotEqual(view1, view3);
            Assert.NotEqual(view2, view3);

        }

        [Fact]
        public void PreventDuplicateColumns()
        {
            Assert.Throws<ArgumentException>(() => View.Create(
                Name,
                Description,
                Measure,
                Mean,
                new List<string>() { "duplicate", "duplicate" }));
        }

        [Fact]
        public void PreventNullViewName()
        {
            Assert.Throws<ArgumentNullException>(() => View.Create(null, Description, Measure, Mean, keys));
        }

        [Fact]
        public void PreventTooLongViewName()
        {
            var chars = new char[ViewName.NameMaxLength + 1];
            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = 'a';
            }

            var longName = new String(chars);
            Assert.Throws<ArgumentOutOfRangeException>(() => ViewName.Create(longName));
        }

        [Fact]
        public void PreventNonPrintableViewName()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ViewName.Create("\u0002"));
        }

        [Fact]
        public void TestViewName()
        {
            Assert.Equal("my name", ViewName.Create("my name").AsString);
        }

        [Fact]
        public void PreventNullNameString()
        {
            Assert.Throws<ArgumentNullException>(() => ViewName.Create(null));
        }

        [Fact]
        public void TestViewNameEquals()
        {
            Assert.Equal(ViewName.Create("view-1"), ViewName.Create("view-1"));
            Assert.NotEqual(ViewName.Create("view-1"), ViewName.Create("view-2"));

        }
    }
}
