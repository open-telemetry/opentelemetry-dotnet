// <copyright file="TracerProviderBuilderExtensionsTest.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Trace.Tests
{
    public class TracerProviderBuilderExtensionsTest
    {
        [Fact]
        public void AddLegacyOperationName_NullBuilder_Noop()
        {
            TracerProviderBuilder builder = null;

            // No exception is thrown on executing this line
            builder.AddLegacyActivity("TestOperationName");
            using var provider = builder.Build();

            var emptyActivitySource = new ActivitySource(string.Empty);
            Assert.False(emptyActivitySource.HasListeners()); // Check if AddLegacyOperationName was noop after TracerProviderBuilder.Build
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void AddLegacyOperationName_BadArgs(string operationName)
        {
            var builder = Sdk.CreateTracerProviderBuilder();
            Assert.Throws<ArgumentException>(() => builder.AddLegacyActivity(operationName));
        }

        [Fact]
        public void AddLegacyOperationNameAddsActivityListenerForEmptyActivitySource()
        {
            var emptyActivitySource = new ActivitySource(string.Empty);
            var builder = Sdk.CreateTracerProviderBuilder();
            builder.AddLegacyActivity("TestOperationName");

            Assert.False(emptyActivitySource.HasListeners());
            using var provider = builder.Build();
            Assert.True(emptyActivitySource.HasListeners());
        }
    }
}
