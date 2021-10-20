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
        }

        [Theory]
        [InlineData(null, false, null)]
        [InlineData("", false, null)] // Environment.SetEnvironmentVariable(EnvVar, ""); clears the environemtal variable as well
        [InlineData("something", true, "something")]
        public void LoadString(string value, bool expectedBool, string expectedValue)
        {
            Environment.SetEnvironmentVariable(EnvVar, value);

            bool actualBool = EnvironmentVariableHelper.LoadString(EnvVar, out string actualValue);

            Assert.Equal(expectedBool, actualBool);
            Assert.Equal(expectedValue, actualValue);
        }

        [Theory]
        [InlineData(null, false, 0)]
        [InlineData("", false, 0)] // Environment.SetEnvironmentVariable(EnvVar, ""); clears the environemtal variable as well
        [InlineData("123", true, 123)]
        [InlineData("0", true, 0)]
        public void LoadNumeric(string value, bool expectedBool, int expectedValue)
        {
            Environment.SetEnvironmentVariable(EnvVar, value);

            bool actualBool = EnvironmentVariableHelper.LoadNumeric(EnvVar, out int actualValue);

            Assert.Equal(expectedBool, actualBool);
            Assert.Equal(expectedValue, actualValue);
        }

        [Theory]
        [InlineData("something")] // NaN
        [InlineData("-12")] // negative number not allowed
        [InlineData("-0")] // sign not allowed
        [InlineData(" 123 ")] // whitespaces not allowed
        [InlineData("0xFF")] // only decimal number allowed
        public void LoadNumeric_Invalid(string value)
        {
            Environment.SetEnvironmentVariable(EnvVar, value);

            Assert.Throws<FormatException>(() => EnvironmentVariableHelper.LoadNumeric(EnvVar, out int _));
        }
    }
}
