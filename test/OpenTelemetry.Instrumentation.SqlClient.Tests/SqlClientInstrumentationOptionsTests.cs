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

using System.Diagnostics;
using System.Linq;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.SqlClient.Tests
{
    public class SqlClientInstrumentationOptionsTests
    {
        static SqlClientInstrumentationOptionsTests()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;

            var listener = new ActivityListener
            {
                ShouldListenTo = _ => true,
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
            };

            ActivitySource.AddActivityListener(listener);
        }

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

        [Theory]
        [InlineData(true, "localhost", "localhost", null, null, null)]
        [InlineData(true, "127.0.0.1,1433", null, "127.0.0.1", null, null)]
        [InlineData(true, "127.0.0.1,1434", null, "127.0.0.1", null, "1434")]
        [InlineData(true, "127.0.0.1\\instanceName, 1818", null, "127.0.0.1", "instanceName", "1818")]
        [InlineData(false, "localhost", "localhost", null, null, null)]
        public void SqlClientInstrumentationOptions_EnableConnectionLevelAttributes(
            bool enableConnectionLevelAttributes,
            string dataSource,
            string expectedServerHostName,
            string expectedServerIpAddress,
            string expectedInstanceName,
            string expectedPort)
        {
            var source = new ActivitySource("sql-client-instrumentation");
            var activity = source.StartActivity("Test Sql Activity");
            var options = new SqlClientInstrumentationOptions
            {
                EnableConnectionLevelAttributes = enableConnectionLevelAttributes,
            };
            options.AddConnectionLevelDetailsToActivity(dataSource, activity);

            if (!enableConnectionLevelAttributes)
            {
                Assert.Equal(expectedServerHostName, activity.Tags.FirstOrDefault(t => t.Key == SemanticConventions.AttributePeerService).Value);
            }
            else
            {
                Assert.Equal(expectedServerHostName, activity.Tags.FirstOrDefault(t => t.Key == SemanticConventions.AttributeNetPeerName).Value);
            }

            Assert.Equal(expectedServerIpAddress, activity.Tags.FirstOrDefault(t => t.Key == SemanticConventions.AttributeNetPeerIp).Value);
            Assert.Equal(expectedInstanceName, activity.Tags.FirstOrDefault(t => t.Key == SemanticConventions.AttributeDbMsSqlInstanceName).Value);
            Assert.Equal(expectedPort, activity.Tags.FirstOrDefault(t => t.Key == SemanticConventions.AttributeNetPeerPort).Value);
        }
    }
}
