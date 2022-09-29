// <copyright file="LoggerOptionsTest.cs" company="OpenTelemetry Authors">
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

using Xunit;

namespace OpenTelemetry.Logs.Tests
{
    public sealed class LoggerOptionsTest
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void VerifyOptionsCannotBeChangedAfterInit(bool initialValue)
        {
            var options = new OpenTelemetryLoggerOptions
            {
                IncludeFormattedMessage = initialValue,
                IncludeScopes = initialValue,
                ParseStateValues = initialValue,
            };
            var provider = new OpenTelemetryLoggerProvider(options);

            // Verify initial set
            Assert.Equal(initialValue, provider.IncludeFormattedMessage);
            Assert.Equal(initialValue, provider.IncludeScopes);
            Assert.Equal(initialValue, provider.ParseStateValues);

            Assert.NotNull(options);

            // Attempt to change value
            options.IncludeFormattedMessage = !initialValue;
            options.IncludeScopes = !initialValue;
            options.ParseStateValues = !initialValue;

            // Verify processor is unchanged
            Assert.Equal(initialValue, provider.IncludeFormattedMessage);
            Assert.Equal(initialValue, provider.IncludeScopes);
            Assert.Equal(initialValue, provider.ParseStateValues);
        }
    }
}
