// <copyright file="CorrelationContextTest.cs" company="OpenTelemetry Authors">
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
using Xunit;

namespace OpenTelemetry.Context.Tests
{
    public class CorrelationContextTest
    {
        private const string K1 = "Key1";
        private const string K2 = "Key2";

        private const string V1 = "Value1";
        private const string V2 = "Value2";

        [Fact]
        public void EmptyContext()
        {
            var cc = CorrelationContext.Current;
            Assert.Empty(cc.Correlations);
            Assert.Equal(CorrelationContext.Empty, cc);

            cc.AddCorrelation(K1, V1);
            Assert.Empty(cc.Correlations);

            Assert.Null(cc.GetCorrelation(K1));
        }

        [Fact]
        public void NonEmptyContext()
        {
            using Activity activity = new Activity("TestActivity");
            activity.Start();

            var list = new List<KeyValuePair<string, string>>(2)
            {
                new KeyValuePair<string, string>(K1, V1),
                new KeyValuePair<string, string>(K2, V2),
            };

            var cc = CorrelationContext.Current;

            cc.AddCorrelation(K1, V1);
            cc.AddCorrelation(K2, V2);

            Assert.NotEqual(CorrelationContext.Empty, cc);
            Assert.Equal(list, cc.Correlations);

            Assert.Equal(V1, cc.GetCorrelation(K1));
            Assert.Null(cc.GetCorrelation(K1.ToLower()));
            Assert.Null(cc.GetCorrelation(K1.ToUpper()));
            Assert.Null(cc.GetCorrelation("NO_KEY"));
        }

        [Fact]
        public void AddExistingKey()
        {
            using Activity activity = new Activity("TestActivity");
            activity.Start();

            var list = new List<KeyValuePair<string, string>>(2)
            {
                new KeyValuePair<string, string>(K1, V1),
                new KeyValuePair<string, string>(K1, V1),
            };

            var cc = CorrelationContext.Current;

            cc.AddCorrelation(K1, V1);
            cc.AddCorrelation(K1, V1);

            Assert.Equal(list, cc.Correlations);
        }

        [Fact]
        public void TestIterator()
        {
            using Activity activity = new Activity("TestActivity");
            activity.Start();

            var list = new List<KeyValuePair<string, string>>(2)
            {
                new KeyValuePair<string, string>(K1, V1),
                new KeyValuePair<string, string>(K2, V2),
            };

            var cc = CorrelationContext.Current;

            cc.AddCorrelation(K1, V1);
            cc.AddCorrelation(K2, V2);

            var i = cc.Correlations.GetEnumerator();

            Assert.True(i.MoveNext());
            var tag1 = i.Current;
            Assert.True(i.MoveNext());
            var tag2 = i.Current;
            Assert.False(i.MoveNext());

            Assert.Equal(list, new List<KeyValuePair<string, string>> { tag1, tag2 });
        }

        [Fact]
        public void TestEquals()
        {
            var cc1 = CreateCorrelationContext(new KeyValuePair<string, string>(K1, V1), new KeyValuePair<string, string>(K2, V2));
            var cc2 = CreateCorrelationContext(new KeyValuePair<string, string>(K1, V1), new KeyValuePair<string, string>(K2, V2));
            var cc3 = CreateCorrelationContext(new KeyValuePair<string, string>(K2, V2), new KeyValuePair<string, string>(K1, V1));
            var cc4 = CreateCorrelationContext(new KeyValuePair<string, string>(K1, V1), new KeyValuePair<string, string>(K2, V1));
            var cc5 = CreateCorrelationContext(new KeyValuePair<string, string>(K1, V2), new KeyValuePair<string, string>(K2, V1));

            Assert.True(cc1.Equals(cc2));

            Assert.False(cc1.Equals(cc3));
            Assert.False(cc1.Equals(cc4));
            Assert.False(cc2.Equals(cc4));
            Assert.False(cc3.Equals(cc4));
            Assert.False(cc5.Equals(cc4));
            Assert.False(cc4.Equals(cc5));
        }

        private static CorrelationContext CreateCorrelationContext(params KeyValuePair<string, string>[] correlations)
        {
            using Activity activity = new Activity("TestActivity");
            activity.Start();

            var cc = CorrelationContext.Current;

            cc.AddCorrelation(correlations);

            return cc;
        }
    }
}
