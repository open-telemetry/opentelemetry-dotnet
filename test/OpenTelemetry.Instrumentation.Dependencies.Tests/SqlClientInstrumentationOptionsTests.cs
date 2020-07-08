// <copyright file="SqlClientInstrumentationOptionsTests.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Instrumentation.Dependencies.Tests
{
    public class SqlClientInstrumentationOptionsTests
    {
        [Theory]
        [InlineData("localhost", "localhost", null, null, null)]
        [InlineData("127.0.0.1", null, "127.0.0.1", null, null)]
        [InlineData("127.0.0.1,1433", null, "127.0.0.1", null, null)]
        [InlineData("127.0.0.1, 1818", null, "127.0.0.1", null, "1818")]
        [InlineData("127.0.0.1  \\  instanceName", null, "127.0.0.1", "instanceName", null)]
        [InlineData("127.0.0.1\\instanceName, 1818", null, "127.0.0.1", "instanceName", "1818")]
        public void ParseDataSourceTests(
            string dataSource,
            string expectedServerHostName,
            string expectedServerIpAddress,
            string expectedInstanceName,
            string expectedPort)
        {
            var sqlConnectionDetails = SqlClientInstrumentationOptions.ParseDataSource(dataSource);

            Assert.NotNull(sqlConnectionDetails);
            Assert.Equal(expectedServerHostName, sqlConnectionDetails.ServerHostName);
            Assert.Equal(expectedServerIpAddress, sqlConnectionDetails.ServerIpAddress);
            Assert.Equal(expectedInstanceName, sqlConnectionDetails.InstanceName);
            Assert.Equal(expectedPort, sqlConnectionDetails.Port);
        }
    }
}
