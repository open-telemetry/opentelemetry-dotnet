// <copyright file="EnvironmentVariableHelperTests.cs" company="OpenTelemetry Authors">
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
using Xunit;

namespace OpenTelemetry.Internal.Tests
{
    public class EnvironmentVariableHelperTests : IDisposable
    {
        private const string EnvVar = "OTEL_EXAMPLE_VARIABLE";

        public EnvironmentVariableHelperTests()
        {
            Environment.SetEnvironmentVariable(EnvVar, null);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(EnvVar, null);
            GC.SuppressFinalize(this);
        }

        [Fact]
        public void LoadString()
        {
            const string value = "something";
            Environment.SetEnvironmentVariable(EnvVar, value);

            bool actualBool = EnvironmentVariableHelper.LoadString(EnvVar, out string actualValue);

            Assert.True(actualBool);
            Assert.Equal(value, actualValue);
        }

        [Fact]
        public void LoadString_NoValue()
        {
            bool actualBool = EnvironmentVariableHelper.LoadString(EnvVar, out string actualValue);

            Assert.False(actualBool);
            Assert.Null(actualValue);
        }

        [Theory]
        [InlineData("123", 123)]
        [InlineData("0", 0)]
        public void LoadNumeric(string value, int expectedValue)
        {
            Environment.SetEnvironmentVariable(EnvVar, value);

            bool actualBool = EnvironmentVariableHelper.LoadNumeric(EnvVar, out int actualValue);

            Assert.True(actualBool);
            Assert.Equal(expectedValue, actualValue);
        }

        [Fact]
        public void LoadNumeric_NoValue()
        {
            bool actualBool = EnvironmentVariableHelper.LoadNumeric(EnvVar, out int actualValue);

            Assert.False(actualBool);
            Assert.Equal(0, actualValue);
        }

        [Theory]
        [InlineData("something")] // NaN
        [InlineData("-12")] // negative number not allowed
        [InlineData("-0")] // sign not allowed
        [InlineData("-1")] // -1 is not allowed
        [InlineData(" 123 ")] // whitespaces not allowed
        [InlineData("0xFF")] // only decimal number allowed
        public void LoadNumeric_Invalid(string value)
        {
            Environment.SetEnvironmentVariable(EnvVar, value);

            Assert.Throws<FormatException>(() => EnvironmentVariableHelper.LoadNumeric(EnvVar, out int _));
        }

        [Theory]
        [InlineData("http://www.example.com", "http://www.example.com/")]
        [InlineData("http://www.example.com/space%20here.html", "http://www.example.com/space here.html")] // characters are converted
        [InlineData("http://www.example.com/space here.html", "http://www.example.com/space here.html")] // characters are escaped
        public void LoadUri(string value, string expectedValue)
        {
            Environment.SetEnvironmentVariable(EnvVar, value);

            bool actualBool = EnvironmentVariableHelper.LoadUri(EnvVar, out Uri actualValue);

            Assert.True(actualBool);
            Assert.Equal(expectedValue, actualValue.ToString());
        }

        [Fact]
        public void LoadUri_NoValue()
        {
            bool actualBool = EnvironmentVariableHelper.LoadUri(EnvVar, out Uri actualValue);

            Assert.False(actualBool);
            Assert.Null(actualValue);
        }

        [Theory]
        [InlineData("invalid")] // invalid format
        [InlineData("  ")] // whitespace
        public void LoadUri_Invalid(string value)
        {
            Environment.SetEnvironmentVariable(EnvVar, value);

            Assert.Throws<FormatException>(() => EnvironmentVariableHelper.LoadUri(EnvVar, out Uri _));
        }
    }
}
