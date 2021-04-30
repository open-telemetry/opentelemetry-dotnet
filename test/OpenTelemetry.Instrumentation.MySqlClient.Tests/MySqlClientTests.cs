// <copyright file="MySqlClientTests.cs" company="OpenTelemetry Authors">
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
using Moq;
using MySql.Data.MySqlClient;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.MySqlClient.Tests
{
    public class MySqlClientTests
    {
        private const string SqlConnectionStringEnvVarName = "OTEL_MYSQLCONNECTIONSTRING";
        private static readonly string SqlConnectionString = SkipUnlessEnvVarFoundTheoryAttribute.GetEnvironmentVariable(SqlConnectionStringEnvVarName);

        [Theory]
        [InlineData("select 1/1", false, false)]
        public void Test(
            string commandText,
            bool setDbStatement = false,
            bool recordException = false)
        {
            var activityProcessor = new Mock<BaseProcessor<Activity>>();
            var sampler = new TestSampler();
            using var shutdownSignal = Sdk.CreateTracerProviderBuilder()
                .AddProcessor(activityProcessor.Object)
                .SetSampler(sampler)
                .AddMySqlClientInstrumentation(options =>
                {
                    options.SetDbStatement = setDbStatement;
                    options.RecordException = recordException;
                })
                .Build();

            using (MySqlConnection mySqlConnection = new MySqlConnection(SqlConnectionString))
            {
                mySqlConnection.Open();

                using MySqlCommand mySqlCommand = new MySqlCommand(commandText, mySqlConnection);

                mySqlCommand.ExecuteNonQuery();
            }

            Assert.Equal(14, activityProcessor.Invocations.Count);
        }
    }
}
