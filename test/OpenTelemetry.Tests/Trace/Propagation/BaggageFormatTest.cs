﻿// <copyright file="BaggageFormatTest.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using Xunit;

namespace OpenTelemetry.Context.Propagation.Tests
{
    public class BaggageFormatTest
    {
        private static readonly Func<IDictionary<string, string>, string, IEnumerable<string>> Getter =
            (d, k) =>
            {
                d.TryGetValue(k, out var v);
                return new string[] { v };
            };

        private static readonly Func<IList<KeyValuePair<string, string>>, string, IEnumerable<string>> GetterList =
            (d, k) =>
            {
                return d.Where(i => i.Key == k).Select(i => i.Value);
            };

        private static readonly Action<IDictionary<string, string>, string, string> Setter = (carrier, name, value) =>
        {
            carrier[name] = value;
        };

        private readonly BaggageFormat baggage = new BaggageFormat();

        [Fact]
        public void ValidateFieldsProperty()
        {
            Assert.Equal(new HashSet<string> { BaggageFormat.BaggageHeaderName }, this.baggage.Fields);
            Assert.Single(this.baggage.Fields);
        }

        [Fact]
        public void ValidateDefaultCarrierExtraction()
        {
            var propagationContext = this.baggage.Extract<string>(default, null, null);
            Assert.Equal(default, propagationContext);
        }

        [Fact]
        public void ValidateDefaultGetterExtraction()
        {
            var carrier = new Dictionary<string, string>();
            var propagationContext = this.baggage.Extract(default, carrier, null);
            Assert.Equal(default, propagationContext);
        }

        [Fact]
        public void ValidateNoBaggageExtraction()
        {
            var carrier = new Dictionary<string, string>();
            var propagationContext = this.baggage.Extract(default, carrier, Getter);
            Assert.Equal(default, propagationContext);
        }

        [Fact]
        public void ValidateOneBaggageExtraction()
        {
            var carrier = new Dictionary<string, string>
            {
                { BaggageFormat.BaggageHeaderName, "name=test" },
            };
            var propagationContext = this.baggage.Extract(default, carrier, Getter);
            Assert.False(propagationContext == default);
            Assert.Single(propagationContext.BaggageContext.GetBaggage());

            var baggage = propagationContext.BaggageContext.GetBaggage().FirstOrDefault();

            Assert.Equal("name", baggage.Key);
            Assert.Equal("test", baggage.Value);
        }

        [Fact]
        public void ValidateMultipleBaggageExtraction()
        {
            var carrier = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>(BaggageFormat.BaggageHeaderName, "name1=test1"),
                new KeyValuePair<string, string>(BaggageFormat.BaggageHeaderName, "name2=test2"),
                new KeyValuePair<string, string>(BaggageFormat.BaggageHeaderName, "name2=test2"),
            };

            var propagationContext = this.baggage.Extract(default, carrier, GetterList);

            Assert.False(propagationContext == default);
            Assert.True(propagationContext.ActivityContext == default);

            Assert.Equal(2, propagationContext.BaggageContext.Count);

            var array = propagationContext.BaggageContext.GetBaggage().ToArray();

            Assert.Equal("name1", array[0].Key);
            Assert.Equal("test1", array[0].Value);

            Assert.Equal("name2", array[1].Key);
            Assert.Equal("test2", array[1].Value);
        }

        [Fact]
        public void ValidateLongBaggageExtraction()
        {
            var carrier = new Dictionary<string, string>
            {
                { BaggageFormat.BaggageHeaderName, $"name={new string('x', 8186)},clientId=1234" },
            };
            var propagationContext = this.baggage.Extract(default, carrier, Getter);
            Assert.False(propagationContext == default);
            Assert.Single(propagationContext.BaggageContext.GetBaggage());

            var array = propagationContext.BaggageContext.GetBaggage().ToArray();

            Assert.Equal("name", array[0].Key);
            Assert.Equal(new string('x', 8186), array[0].Value);
        }

        [Fact]
        public void ValidateEmptyBaggageInjection()
        {
            var carrier = new Dictionary<string, string>();
            this.baggage.Inject(default, carrier, Setter);

            Assert.Empty(carrier);
        }

        [Fact]
        public void ValidateBaggageInjection()
        {
            var carrier = new Dictionary<string, string>();
            var propagationContext = new PropagationContext(
                default,
                new BaggageContext(new Dictionary<string, string>
                {
                    { "key1", "value1" },
                    { "key2", "value2" },
                }));

            this.baggage.Inject(propagationContext, carrier, Setter);

            Assert.Single(carrier);
            Assert.Equal("key1=value1,key2=value2", carrier[BaggageFormat.BaggageHeaderName]);
        }
    }
}
