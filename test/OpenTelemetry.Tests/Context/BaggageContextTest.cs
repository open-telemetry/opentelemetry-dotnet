// <copyright file="BaggageContextTest.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;
using Xunit;

namespace OpenTelemetry.Context.Tests
{
    public class BaggageContextTest
    {
        private const string K1 = "Key1";
        private const string K2 = "Key2";
        private const string K3 = "Key3";

        private const string V1 = "Value1";
        private const string V2 = "Value2";
        private const string V3 = "Value3";

        [Fact]
        public void EmptyContextTest()
        {
            Assert.Empty(BaggageContext.GetBaggage());
            Assert.Empty(BaggageContext.Current.GetBaggage());
        }

        [Fact]
        public void AddAndGetContextTest()
        {
            var list = new List<KeyValuePair<string, string>>(2)
            {
                new KeyValuePair<string, string>(K1, V1),
                new KeyValuePair<string, string>(K2, V2),
            };

            BaggageContext.SetBaggage(K1, V1);
            BaggageContext.Current.SetBaggage(K2, V2);

            Assert.NotEmpty(BaggageContext.GetBaggage());
            Assert.Equal(list, BaggageContext.GetBaggage(BaggageContext.Current));

            Assert.Equal(V1, BaggageContext.GetBaggage(K1));
            Assert.Equal(V1, BaggageContext.GetBaggage(K1.ToLower()));
            Assert.Equal(V1, BaggageContext.GetBaggage(K1.ToUpper()));
            Assert.Null(BaggageContext.GetBaggage("NO_KEY"));
            Assert.Equal(V2, BaggageContext.Current.GetBaggage(K2));

            Assert.Throws<ArgumentNullException>(() => BaggageContext.GetBaggage(null));
        }

        [Fact]
        public void AddExistingKeyTest()
        {
            var list = new List<KeyValuePair<string, string>>(2)
            {
                new KeyValuePair<string, string>(K1, V1),
            };

            BaggageContext.Current.SetBaggage(new KeyValuePair<string, string>(K1, V1));
            var baggage = BaggageContext.SetBaggage(K1, V1);
            BaggageContext.SetBaggage(new Dictionary<string, string> { [K1] = V1 }, baggage);

            Assert.Equal(list, BaggageContext.GetBaggage());
        }

        [Fact]
        public void AddNullValueTest()
        {
            var baggage = BaggageContext.Current;
            baggage = BaggageContext.SetBaggage(K1, V1, baggage);

            Assert.Equal(1, BaggageContext.Current.Count);
            Assert.Equal(1, baggage.Count);

            BaggageContext.Current.SetBaggage(K2, null);

            Assert.Equal(1, BaggageContext.Current.Count);

            Assert.Empty(BaggageContext.SetBaggage(K1, null).GetBaggage());

            BaggageContext.SetBaggage(K1, V1);
            BaggageContext.SetBaggage(new Dictionary<string, string>
            {
                [K1] = null,
                [K2] = V2,
            });
            Assert.Equal(1, BaggageContext.Current.Count);
            Assert.Contains(BaggageContext.GetBaggage(), kvp => kvp.Key == K2);
        }

        [Fact]
        public void RemoveTest()
        {
            var empty = BaggageContext.Current;
            var empty2 = BaggageContext.RemoveBaggage(K1);
            Assert.True(empty == empty2);

            var context = BaggageContext.SetBaggage(new Dictionary<string, string>
            {
                [K1] = V1,
                [K2] = V2,
                [K3] = V3,
            });

            var context2 = BaggageContext.RemoveBaggage(K1, context);

            Assert.Equal(3, context.Count);
            Assert.Equal(2, context2.Count);

            Assert.DoesNotContain(new KeyValuePair<string, string>(K1, V1), context2.GetBaggage());
        }

        [Fact]
        public void ClearTest()
        {
            var context = BaggageContext.SetBaggage(new Dictionary<string, string>
            {
                [K1] = V1,
                [K2] = V2,
                [K3] = V3,
            });

            Assert.Equal(3, context.Count);

            BaggageContext.ClearBaggage();

            Assert.Equal(0, BaggageContext.Current.Count);
        }

        [Fact]
        public void ContextFlowTest()
        {
            var context = BaggageContext.SetBaggage(K1, V1);
            var context2 = BaggageContext.Current.SetBaggage(K2, V2);
            var context3 = BaggageContext.SetBaggage(K3, V3);

            Assert.Equal(1, context.Count);
            Assert.Equal(2, context2.Count);
            Assert.Equal(3, context3.Count);

            BaggageContext.Current = context;

            var context4 = BaggageContext.SetBaggage(K3, V3);

            Assert.Equal(2, context4.Count);
            Assert.DoesNotContain(new KeyValuePair<string, string>(K2, V2), context4.GetBaggage());
        }

        [Fact]
        public void EnumeratorTest()
        {
            var list = new List<KeyValuePair<string, string>>(2)
            {
                new KeyValuePair<string, string>(K1, V1),
                new KeyValuePair<string, string>(K2, V2),
            };

            var baggage = BaggageContext.SetBaggage(K1, V1);
            baggage = BaggageContext.SetBaggage(K2, V2, baggage);

            var enumerator = BaggageContext.GetEnumerator(baggage);

            Assert.True(enumerator.MoveNext());
            var tag1 = enumerator.Current;
            Assert.True(enumerator.MoveNext());
            var tag2 = enumerator.Current;
            Assert.False(enumerator.MoveNext());

            Assert.Equal(list, new List<KeyValuePair<string, string>> { tag1, tag2 });

            BaggageContext.ClearBaggage();

            enumerator = BaggageContext.GetEnumerator();

            Assert.False(enumerator.MoveNext());
        }

        [Fact]
        public void EqualsTest()
        {
            var bc1 = new BaggageContext(new Dictionary<string, string>() { [K1] = V1, [K2] = V2 });
            var bc2 = new BaggageContext(new Dictionary<string, string>() { [K1] = V1, [K2] = V2 });
            var bc3 = new BaggageContext(new Dictionary<string, string>() { [K2] = V2, [K1] = V1 });
            var bc4 = new BaggageContext(new Dictionary<string, string>() { [K1] = V1, [K2] = V1 });
            var bc5 = new BaggageContext(new Dictionary<string, string>() { [K1] = V2, [K2] = V1 });

            Assert.True(bc1.Equals(bc2));

            Assert.False(bc1.Equals(bc3));
            Assert.False(bc1.Equals(bc4));
            Assert.False(bc2.Equals(bc4));
            Assert.False(bc3.Equals(bc4));
            Assert.False(bc5.Equals(bc4));
            Assert.False(bc4.Equals(bc5));
        }

        [Fact]
        public void CreateBaggageTest()
        {
            var baggage = BaggageContext.Create(null);

            Assert.Equal(default, baggage);

            baggage = BaggageContext.Create(new Dictionary<string, string>
            {
                [K1] = V1,
                ["key2"] = "value2",
                ["KEY2"] = "VALUE2",
                ["KEY3"] = "VALUE3",
                ["Key3"] = null,
            });

            Assert.Equal(2, baggage.Count);
            Assert.Contains(baggage.GetBaggage(), kvp => kvp.Key == K1);
            Assert.Equal("VALUE2", BaggageContext.GetBaggage("key2", baggage));
        }

        [Fact]
        public void EqualityTests()
        {
            var emptyBaggage = BaggageContext.Create(null);

            var baggage = BaggageContext.SetBaggage(K1, V1);

            Assert.NotEqual(emptyBaggage, baggage);

            Assert.True(emptyBaggage != baggage);

            baggage = BaggageContext.ClearBaggage(baggage);

            Assert.Equal(emptyBaggage, baggage);

            baggage = BaggageContext.SetBaggage(K1, V1);

            var baggage2 = BaggageContext.SetBaggage(null);

            Assert.Equal(baggage, baggage2);

            Assert.False(baggage.Equals(this));
            Assert.True(baggage.Equals((object)baggage2));
        }

        [Fact]
        public void GetHashCodeTests()
        {
            var baggage = BaggageContext.Current;
            var emptyBaggage = BaggageContext.Create(null);

            Assert.Equal(emptyBaggage.GetHashCode(), baggage.GetHashCode());

            baggage = BaggageContext.SetBaggage(K1, V1, baggage);

            Assert.NotEqual(emptyBaggage.GetHashCode(), baggage.GetHashCode());

            var expectedBaggage = BaggageContext.Create(new Dictionary<string, string> { [K1] = V1 });

            Assert.Equal(expectedBaggage.GetHashCode(), baggage.GetHashCode());
        }
    }
}
