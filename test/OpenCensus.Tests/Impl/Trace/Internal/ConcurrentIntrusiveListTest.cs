// <copyright file="ConcurrentIntrusiveListTest.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Trace.Internal.Test
{
    using System;
    using OpenCensus.Utils;
    using Xunit;

    public class ConcurrentIntrusiveListTest
    {
        private readonly ConcurrentIntrusiveList<FakeElement> intrusiveList = new ConcurrentIntrusiveList<FakeElement>();

        [Fact]
        public void EmptyList()
        {
            Assert.Equal(0, intrusiveList.Count);
            Assert.Empty(intrusiveList.Copy());
        }

        [Fact]
        public void AddRemoveAdd_SameElement()
        {
            FakeElement element = new FakeElement();
            intrusiveList.AddElement(element);
            Assert.Equal(1, intrusiveList.Count);
            intrusiveList.RemoveElement(element);
            Assert.Equal(0, intrusiveList.Count);
            intrusiveList.AddElement(element);
            Assert.Equal(1, intrusiveList.Count);
        }

        [Fact]
        public void addAndRemoveElements()
        {
            FakeElement element1 = new FakeElement();
            FakeElement element2 = new FakeElement();
            FakeElement element3 = new FakeElement();
            intrusiveList.AddElement(element1);
            intrusiveList.AddElement(element2);
            intrusiveList.AddElement(element3);
            Assert.Equal(3, intrusiveList.Count);
            var copy = intrusiveList.Copy();
            Assert.Equal(element3, copy[0]);
            Assert.Equal(element2, copy[1]);
            Assert.Equal(element1, copy[2]);
            // Remove element from the middle of the list.
            intrusiveList.RemoveElement(element2);
            Assert.Equal(2, intrusiveList.Count);
            copy = intrusiveList.Copy();
            Assert.Equal(element3, copy[0]);
            Assert.Equal(element1, copy[1]);
            // Remove element from the tail of the list.
            intrusiveList.RemoveElement(element1);
            Assert.Equal(1, intrusiveList.Count);
            copy = intrusiveList.Copy();
            Assert.Equal(element3, copy[0]);

            intrusiveList.AddElement(element1);
            Assert.Equal(2, intrusiveList.Count);
            copy = intrusiveList.Copy();
            Assert.Equal(element1, copy[0]);
            Assert.Equal(element3, copy[1]);
            // Remove element from the head of the list when there are other elements after.
            intrusiveList.RemoveElement(element1);
            Assert.Equal(1, intrusiveList.Count);
            copy = intrusiveList.Copy();
            Assert.Equal(element3, copy[0]);
            // Remove element from the head of the list when no more other elements in the list.
            intrusiveList.RemoveElement(element3);
            Assert.Equal(0, intrusiveList.Count);
            Assert.Empty(intrusiveList.Copy());
        }

        [Fact]
        public void AddAlreadyAddedElement()
        {
            FakeElement element = new FakeElement();
            intrusiveList.AddElement(element);

            Assert.Throws<ArgumentOutOfRangeException>(() => intrusiveList.AddElement(element));
        }

        [Fact]
        public void removeNotAddedElement()
        {
            FakeElement element = new FakeElement();
            Assert.Throws<ArgumentOutOfRangeException>(() => intrusiveList.RemoveElement(element));
        }


        private sealed class FakeElement : IElement<FakeElement>
        {
            public FakeElement Next { get; set; }

            public FakeElement Previous { get; set; }
        }
    }
}
