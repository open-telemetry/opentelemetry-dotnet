// <copyright file="GuardTest.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using OpenTelemetry.Internal;
using Xunit;

namespace OpenTelemetry.Tests.Internal
{
#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name
    public class Thing
#pragma warning restore SA1649 // File name should match first type name
#pragma warning restore SA1402 // File may only contain a single type
    {
        public string Bar { get; set; }
    }

    public class GuardTest
    {
        [Fact]
        public void NullTest()
        {
            // Valid
            Guard.ThrowIfNull(1);
            Guard.ThrowIfNull(1.0);
            Guard.ThrowIfNull(new object());
            Guard.ThrowIfNull("hello");

            // Invalid
            object potato = null;
            var ex1 = Assert.Throws<ArgumentNullException>(() => Guard.ThrowIfNull(potato));
            Assert.Contains("Must not be null", ex1.Message);
            Assert.Equal("potato", ex1.ParamName);

            object @event = null;
            var ex2 = Assert.Throws<ArgumentNullException>(() => Guard.ThrowIfNull(@event));
            Assert.Contains("Must not be null", ex2.Message);
            Assert.Equal("@event", ex2.ParamName);

            Thing thing = null;
            var ex3 = Assert.Throws<ArgumentNullException>(() => Guard.ThrowIfNull(thing?.Bar));
            Assert.Contains("Must not be null", ex3.Message);
            Assert.Equal("thing?.Bar", ex3.ParamName);
        }

        [Fact]
        public void NullOrEmptyTest()
        {
            // Valid
            Guard.ThrowIfNullOrEmpty("a");
            Guard.ThrowIfNullOrEmpty(" ");

            // Invalid
            var ex1 = Assert.Throws<ArgumentException>(() => Guard.ThrowIfNullOrEmpty(null));
            Assert.Contains("Must not be null or empty", ex1.Message);
            Assert.Equal("null", ex1.ParamName);

            var ex2 = Assert.Throws<ArgumentException>(() => Guard.ThrowIfNullOrEmpty(string.Empty));
            Assert.Contains("Must not be null or empty", ex2.Message);
            Assert.Equal("string.Empty", ex2.ParamName);

            var x = string.Empty;
            var ex3 = Assert.Throws<ArgumentException>(() => Guard.ThrowIfNullOrEmpty(x));
            Assert.Contains("Must not be null or empty", ex3.Message);
            Assert.Equal("x", ex3.ParamName);
        }

        [Fact]
        public void NullOrWhitespaceTest()
        {
            // Valid
            Guard.ThrowIfNullOrWhitespace("a");

            // Invalid
            var ex1 = Assert.Throws<ArgumentException>(() => Guard.ThrowIfNullOrWhitespace(null));
            Assert.Contains("Must not be null or whitespace", ex1.Message);
            Assert.Equal("null", ex1.ParamName);

            var ex2 = Assert.Throws<ArgumentException>(() => Guard.ThrowIfNullOrWhitespace(string.Empty));
            Assert.Contains("Must not be null or whitespace", ex2.Message);
            Assert.Equal("string.Empty", ex2.ParamName);

            var ex3 = Assert.Throws<ArgumentException>(() => Guard.ThrowIfNullOrWhitespace(" \t\n\r"));
            Assert.Contains("Must not be null or whitespace", ex3.Message);
            Assert.Equal("\" \\t\\n\\r\"", ex3.ParamName);
        }

        [Fact]
        public void InvalidTimeoutTest()
        {
            // Valid
            Guard.ThrowIfInvalidTimeout(Timeout.Infinite);
            Guard.ThrowIfInvalidTimeout(0);
            Guard.ThrowIfInvalidTimeout(100);

            // Invalid
            var ex1 = Assert.Throws<ArgumentOutOfRangeException>(() => Guard.ThrowIfInvalidTimeout(-100));
            Assert.Contains("Must be non-negative or 'Timeout.Infinite'", ex1.Message);
            Assert.Equal("-100", ex1.ParamName);
        }

        [Fact]
        public void RangeIntTest()
        {
            // Valid
            Guard.ThrowIfOutOfRange(0);
            Guard.ThrowIfOutOfRange(0, min: 0, max: 0);
            Guard.ThrowIfOutOfRange(5, min: -10, max: 10);
            Guard.ThrowIfOutOfRange(int.MinValue, min: int.MinValue, max: 10);
            Guard.ThrowIfOutOfRange(int.MaxValue, min: 10, max: int.MaxValue);

            // Invalid
            var ex1 = Assert.Throws<ArgumentOutOfRangeException>(() => Guard.ThrowIfOutOfRange(-1, min: 0, max: 100, minName: "empty", maxName: "full"));
            Assert.Contains("Must be in the range: [0: empty, 100: full]", ex1.Message);

            var ex2 = Assert.Throws<ArgumentOutOfRangeException>(() => Guard.ThrowIfOutOfRange(-1, min: 0, max: 100, message: "error"));
            Assert.Contains("error", ex2.Message);
        }

        [Fact]
        public void RangeDoubleTest()
        {
            // Valid
            Guard.ThrowIfOutOfRange(1.0, min: 1.0, max: 1.0);
            Guard.ThrowIfOutOfRange(double.MinValue, min: double.MinValue, max: 10.0);
            Guard.ThrowIfOutOfRange(double.MaxValue, min: 10.0, max: double.MaxValue);

            // Invalid
            var ex3 = Assert.Throws<ArgumentOutOfRangeException>(() => Guard.ThrowIfOutOfRange(-1.1, min: 0.1, max: 99.9, minName: "empty", maxName: "full"));
            Assert.Contains("Must be in the range: [0.1: empty, 99.9: full]", ex3.Message);

            var ex4 = Assert.Throws<ArgumentOutOfRangeException>(() => Guard.ThrowIfOutOfRange(-1.1, min: 0.0, max: 100.0));
            Assert.Contains("Must be in the range: [0, 100]", ex4.Message);
        }

        [Fact]
        public void TypeTest()
        {
            // Valid
            Guard.ThrowIfNotOfType<int>(0);
            Guard.ThrowIfNotOfType<object>(new object());
            Guard.ThrowIfNotOfType<string>("hello");

            // Invalid
            var ex1 = Assert.Throws<InvalidCastException>(() => Guard.ThrowIfNotOfType<double>(100));
            Assert.Equal("Cannot cast '100' from 'Int32' to 'Double'", ex1.Message);
        }

        [Fact]
        public void ZeroTest()
        {
            // Valid
            Guard.ThrowIfZero(1);

            // Invalid
            var ex1 = Assert.Throws<ArgumentException>(() => Guard.ThrowIfZero(0));
            Assert.Contains("Must not be zero", ex1.Message);
            Assert.Equal("0", ex1.ParamName);
        }
    }
}
