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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Moq;
using MySql.Data.MySqlClient;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.MySqlClient.Tests
{
    public class MySqlClientTests
    {
        /*
            To run the integration tests, set the OTEL_MYSQLCONNECTIONSTRING machine-level environment variable to a valid Sql Server connection string.

            To use Docker...
             1) Run: docker run -d --name mysql8 -e "MYSQL_ROOT_PASSWORD=Pass@word" -p 3306:3306 mysql:8
             2) Set OTEL_MYSQLCONNECTIONSTRING as: Database=mysql;Data Source=127.0.0.1;User Id=root;Password=Pass@word;port=3306;Pooling=false;
         */
        private const string MySqlConnectionStringEnvVarName = "OTEL_MYSQLCONNECTIONSTRING";

        private static readonly string MySqlConnectionString = SkipUnlessEnvVarFoundTheoryAttribute.GetEnvironmentVariable(MySqlConnectionStringEnvVarName);

        [Trait("CategoryName", "MySqlIntegrationTests")]
        [SkipUnlessEnvVarFoundTheory(MySqlConnectionStringEnvVarName)]
        [InlineData("select 1/1", true, true, true, false)]
        [InlineData("select 1/1", true, true, false, false)]
        [InlineData("selext 1/1", true, true, true, true)]
        [InlineData("select 1/0", true, true, true, false, true)]
        public void Test(
            string commandText,
            bool setDbStatement = false,
            bool recordException = false,
            bool enableConnectionLevelAttributes = false,
            bool isFailure = false,
            bool warning = false)
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
                    options.EnableConnectionLevelAttributes = enableConnectionLevelAttributes;
                })
                .Build();

            var connectionStringBuilder = new MySqlConnectionStringBuilder(MySqlConnectionString);
            connectionStringBuilder.Pooling = false;
            using MySqlConnection mySqlConnection = new MySqlConnection(MySqlConnectionString);
            var dataSource = mySqlConnection.DataSource;
            mySqlConnection.Open();
            mySqlConnection.ChangeDatabase("mysql");

            using MySqlCommand mySqlCommand = new MySqlCommand(commandText, mySqlConnection);

            try
            {
                mySqlCommand.ExecuteNonQuery();
            }
            catch
            {
            }

            Activity activity;

            // select 1/0 will cause the driver execute `SHOW WARNINGS`
            if (warning)
            {
                Assert.Equal(14, activityProcessor.Invocations.Count);
                activity = (Activity)activityProcessor.Invocations[11].Arguments[0];
            }
            else
            {
                Assert.Equal(13, activityProcessor.Invocations.Count);
                activity = (Activity)activityProcessor.Invocations[11].Arguments[0];
            }

            VerifyActivityData(commandText, setDbStatement, recordException, enableConnectionLevelAttributes, isFailure, dataSource, activity);
        }

        private static void VerifyActivityData(
            string commandText,
            bool setDbStatement,
            bool recordException,
            bool enableConnectionLevelAttributes,
            bool isFailure,
            string dataSource,
            Activity activity)
        {
            if (!isFailure)
            {
                Assert.Equal(Status.Unset, activity.GetStatus());
            }
            else
            {
                var status = activity.GetStatus();
                Assert.Equal(Status.Error.StatusCode, status.StatusCode);
                Assert.NotNull(status.Description);

                if (recordException)
                {
                    var events = activity.Events.ToList();
                    Assert.Single(events);

                    Assert.Equal(SemanticConventions.AttributeExceptionEventName, events[0].Name);
                }
                else
                {
                    Assert.Empty(activity.Events);
                }
            }

            Assert.Equal("mysql", activity.GetTagValue(SemanticConventions.AttributeDbName));

            if (setDbStatement)
            {
                Assert.Equal(commandText, activity.GetTagValue(SemanticConventions.AttributeDbStatement));
            }
            else
            {
                Assert.Null(activity.GetTagValue(SemanticConventions.AttributeDbStatement));
            }

            if (!enableConnectionLevelAttributes)
            {
                Assert.Equal(dataSource, activity.GetTagValue(SemanticConventions.AttributePeerService));
            }
            else
            {
                var uriHostNameType = Uri.CheckHostName(dataSource);
                if (uriHostNameType == UriHostNameType.IPv4 || uriHostNameType == UriHostNameType.IPv6)
                {
                    Assert.Equal(dataSource, activity.GetTagValue(SemanticConventions.AttributeNetPeerIp));
                }
                else
                {
                    Assert.Equal(dataSource, activity.GetTagValue(SemanticConventions.AttributeNetPeerName));
                }
            }
        }
    }
}
