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

namespace OpenTelemetry.Tests.Shared
{
    public static class GuardTest
    {
        [Fact]
        public static void NotNullTest_Throws()
        {
            var ex1 = Assert.Throws<ArgumentNullException>(() => Guard.NotNull(null, "null"));
            Assert.Contains("Must not be null", ex1.Message);
        }

        [Fact]
        public static void NotNullTest_NoThrow()
        {
            Guard.NotNull(1);
            Guard.NotNull(1.0);
            Guard.NotNull(new object());
            Guard.NotNull("hello");
        }

        [Fact]
        public static void NotNullOrEmptyTest_Throws()
        {
            var ex1 = Assert.Throws<ArgumentException>(() => Guard.NotNullOrEmpty(null));
            Assert.Contains("Must not be null or empty", ex1.Message);

            var ex2 = Assert.Throws<ArgumentException>(() => Guard.NotNullOrEmpty(string.Empty));
            Assert.Contains("Must not be null or empty", ex2.Message);
        }

        [Fact]
        public static void NotNullOrEmptyTest_NoThrow()
        {
            Guard.NotNullOrEmpty("a");
            Guard.NotNullOrEmpty(" ");
        }

        [Fact]
        public static void NotNullOrWhitespaceTest_Throws()
        {
            var ex1 = Assert.Throws<ArgumentException>(() => Guard.NotNullOrWhitespace(null));
            Assert.Contains("Must not be null or whitespace", ex1.Message);

            var ex2 = Assert.Throws<ArgumentException>(() => Guard.NotNullOrWhitespace(string.Empty));
            Assert.Contains("Must not be null or whitespace", ex2.Message);

            var ex3 = Assert.Throws<ArgumentException>(() => Guard.NotNullOrWhitespace(" \t\n\r"));
            Assert.Contains("Must not be null or whitespace", ex3.Message);
        }

        [Fact]
        public static void NotNullOrWhitespaceTest_NoThrow()
        {
            Guard.NotNullOrWhitespace("a");
        }

        [Fact]
        public static void NotValidTimeoutTest_Throws()
        {
            var ex1 = Assert.Throws<ArgumentOutOfRangeException>(() => Guard.NotValidTimeout(-100));
            Assert.Contains("Must be non-negative or 'Timeout.Infinite'", ex1.Message);
        }

        [Fact]
        public static void NotValidTimeoutTest_NoThrow()
        {
            Guard.NotValidTimeout(Timeout.Infinite);
            Guard.NotValidTimeout(0);
            Guard.NotValidTimeout(100);
        }

        [Fact]
        public static void NotInRangeTest_Throws()
        {
            // Int
            var ex1 = Assert.Throws<ArgumentOutOfRangeException>(() => Guard.NotInRange(-1, min: 0, max: 100, minName: "empty", maxName: "full"));
            Assert.Contains("Must be in the range: [0: empty, 100: full]", ex1.Message);

            var ex2 = Assert.Throws<ArgumentOutOfRangeException>(() => Guard.NotInRange(-1, min: 0, max: 100, message: "error"));
            Assert.Contains("error", ex2.Message);

            // Double
            var ex3 = Assert.Throws<ArgumentOutOfRangeException>(() => Guard.NotInRange(-1.1, min: 0.1, max: 99.9, minName: "empty", maxName: "full"));
            Assert.Contains("Must be in the range: [0.1: empty, 99.9: full]", ex3.Message);

            var ex4 = Assert.Throws<ArgumentOutOfRangeException>(() => Guard.NotInRange(-1.1, min: 0.0, max: 100.0));
            Assert.Contains("Must be in the range: [0, 100]", ex4.Message);
        }

        [Fact]
        public static void NotInRangeTest_NoThrow()
        {
            // Int
            Guard.NotInRange(0);
            Guard.NotInRange(0, min: 0, max: 0);
            Guard.NotInRange(5, min: -10, max: 10);
            Guard.NotInRange(int.MinValue, min: int.MinValue, max: 10);
            Guard.NotInRange(int.MaxValue, min: 10, max: int.MaxValue);

            // Double
            Guard.NotInRange(1.0, min: 1.0, max: 1.0);
            Guard.NotInRange(double.MinValue, min: double.MinValue, max: 10.0);
            Guard.NotInRange(double.MaxValue, min: 10.0, max: double.MaxValue);
        }

        [Fact]
        public static void NotOfTypeTest_Throws()
        {
            var ex1 = Assert.Throws<InvalidCastException>(() => Guard.NotOfType<double>(100));
            Assert.Equal("Cannot cast 'N/A' from 'Int32' to 'Double'", ex1.Message);
        }

        [Fact]
        public static void NotOfType_NoThrow()
        {
            Guard.NotOfType<int>(0);
            Guard.NotOfType<object>(new object());
            Guard.NotOfType<string>("hello");
        }
    }
}
