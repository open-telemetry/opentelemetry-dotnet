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
    public static class GuardTest
    {
        [Fact]
        public static void NullTest()
        {
            // Valid
            Guard.Null(1);
            Guard.Null(1.0);
            Guard.Null(new object());
            Guard.Null("hello");

            // Invalid
            var ex1 = Assert.Throws<ArgumentNullException>(() => Guard.Null(null, "null"));
            Assert.Contains("Must not be null", ex1.Message);
        }

        [Fact]
        public static void NullOrEmptyTest()
        {
            // Valid
            Guard.NullOrEmpty("a");
            Guard.NullOrEmpty(" ");

            // Invalid
            var ex1 = Assert.Throws<ArgumentException>(() => Guard.NullOrEmpty(null));
            Assert.Contains("Must not be null or empty", ex1.Message);

            var ex2 = Assert.Throws<ArgumentException>(() => Guard.NullOrEmpty(string.Empty));
            Assert.Contains("Must not be null or empty", ex2.Message);
        }

        [Fact]
        public static void NullOrWhitespaceTest()
        {
            // Valid
            Guard.NullOrWhitespace("a");

            // Invalid
            var ex1 = Assert.Throws<ArgumentException>(() => Guard.NullOrWhitespace(null));
            Assert.Contains("Must not be null or whitespace", ex1.Message);

            var ex2 = Assert.Throws<ArgumentException>(() => Guard.NullOrWhitespace(string.Empty));
            Assert.Contains("Must not be null or whitespace", ex2.Message);

            var ex3 = Assert.Throws<ArgumentException>(() => Guard.NullOrWhitespace(" \t\n\r"));
            Assert.Contains("Must not be null or whitespace", ex3.Message);
        }

        [Fact]
        public static void InvalidTimeoutTest()
        {
            // Valid
            Guard.InvalidTimeout(Timeout.Infinite);
            Guard.InvalidTimeout(0);
            Guard.InvalidTimeout(100);

            // Invalid
            var ex1 = Assert.Throws<ArgumentOutOfRangeException>(() => Guard.InvalidTimeout(-100));
            Assert.Contains("Must be non-negative or 'Timeout.Infinite'", ex1.Message);
        }

        [Fact]
        public static void RangeIntTest()
        {
            // Valid
            Guard.Range(0);
            Guard.Range(0, min: 0, max: 0);
            Guard.Range(5, min: -10, max: 10);
            Guard.Range(int.MinValue, min: int.MinValue, max: 10);
            Guard.Range(int.MaxValue, min: 10, max: int.MaxValue);

            // Invalid
            var ex1 = Assert.Throws<ArgumentOutOfRangeException>(() => Guard.Range(-1, min: 0, max: 100, minName: "empty", maxName: "full"));
            Assert.Contains("Must be in the range: [0: empty, 100: full]", ex1.Message);

            var ex2 = Assert.Throws<ArgumentOutOfRangeException>(() => Guard.Range(-1, min: 0, max: 100, message: "error"));
            Assert.Contains("error", ex2.Message);
        }

        [Fact]
        public static void RangeDoubleTest()
        {
            // Valid
            Guard.Range(1.0, min: 1.0, max: 1.0);
            Guard.Range(double.MinValue, min: double.MinValue, max: 10.0);
            Guard.Range(double.MaxValue, min: 10.0, max: double.MaxValue);

            // Invalid
            var ex3 = Assert.Throws<ArgumentOutOfRangeException>(() => Guard.Range(-1.1, min: 0.1, max: 99.9, minName: "empty", maxName: "full"));
            Assert.Contains("Must be in the range: [0.1: empty, 99.9: full]", ex3.Message);

            var ex4 = Assert.Throws<ArgumentOutOfRangeException>(() => Guard.Range(-1.1, min: 0.0, max: 100.0));
            Assert.Contains("Must be in the range: [0, 100]", ex4.Message);
        }

        [Fact]
        public static void TypeTest()
        {
            // Valid
            Guard.Type<int>(0);
            Guard.Type<object>(new object());
            Guard.Type<string>("hello");

            // Invalid
            var ex1 = Assert.Throws<InvalidCastException>(() => Guard.Type<double>(100));
            Assert.Equal("Cannot cast 'N/A' from 'Int32' to 'Double'", ex1.Message);
        }

        [Fact]
        public static void ZeroTest()
        {
            // Valid
            Guard.Zero(1);

            // Invalid
            var ex1 = Assert.Throws<ArgumentException>(() => Guard.Zero(0));
            Assert.Contains("Must not be zero", ex1.Message);
        }
    }
}
