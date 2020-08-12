// <copyright file="BaggageFormatTest.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Context.Propagation.Test
{
    public class BaggageFormatTest
    {
        private static readonly Func<IDictionary<string, string>, string, IEnumerable<string>> Getter =
            (d, k) =>
            {
                d.TryGetValue(k, out var v);
                return new string[] { v };
            };

        private static readonly Action<IDictionary<string, string>, string, string> Setter = (carrier, name, value) =>
        {
            carrier[name] = value;
        };

        private readonly BaggageFormat baggage = new BaggageFormat();

        [Fact]
        public void ValidateFieldsProperty()
        {
            Assert.Equal(new HashSet<string> { "baggage" }, this.baggage.Fields);
            Assert.Single(this.baggage.Fields);
        }

        [Fact]
        public void ValidateDefaultCarrierExtraction()
        {
            var textFormatContext = this.baggage.Extract<string>(default, null, null);
            Assert.Equal(default, textFormatContext);
        }

        [Fact]
        public void ValidateDefaultGetterExtraction()
        {
            var carrier = new Dictionary<string, string>();
            var textFormatContext = this.baggage.Extract(default, carrier, null);
            Assert.Equal(default, textFormatContext);
        }

        [Fact]
        public void ValidateNoBaggageExtraction()
        {
            var carrier = new Dictionary<string, string>();
            var textFormatContext = this.baggage.Extract(default, carrier, Getter);
            Assert.Equal(default, textFormatContext);
        }

        [Fact]
        public void ValidateOneBaggageExtraction()
        {
            var carrier = new Dictionary<string, string>
            {
                { "baggage", "name=test" },
            };
            var textFormatContext = this.baggage.Extract(default, carrier, Getter);
            Assert.False(textFormatContext == default);
            Assert.Single(textFormatContext.ActivityBaggage);

            var array = textFormatContext.ActivityBaggage.ToArray();

            Assert.Equal("name", array[0].Key);
            Assert.Equal("test", array[0].Value);
        }

        [Fact]
        public void ValidateLongBaggageExtraction()
        {
            var carrier = new Dictionary<string, string>
            {
                { "baggage", $"name={new string('x', 1018)},clientId=1234" },
            };
            var textFormatContext = this.baggage.Extract(default, carrier, Getter);
            Assert.False(textFormatContext == default);
            Assert.Single(textFormatContext.ActivityBaggage);

            var array = textFormatContext.ActivityBaggage.ToArray();

            Assert.Equal("name", array[0].Key);
            Assert.Equal(new string('x', 1018), array[0].Value);
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
            var textFormatContext = new TextFormatContext(default, new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key2", "value2" },
            });

            this.baggage.Inject(textFormatContext, carrier, Setter);

            Assert.Single(carrier);
            Assert.Equal("key1=value1,key2=value2", carrier["baggage"]);
        }
    }
}
