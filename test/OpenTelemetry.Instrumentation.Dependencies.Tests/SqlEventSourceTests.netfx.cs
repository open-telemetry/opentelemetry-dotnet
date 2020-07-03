// <copyright file="SqlEventSourceTests.netfx.cs" company="OpenTelemetry Authors">
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
#if NETFRAMEWORK
using System;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using OpenTelemetry.Instrumentation.Dependencies.Implementation;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;
using Xunit;

namespace OpenTelemetry.Instrumentation.Dependencies.Tests
{
    public class SqlEventSourceTests
    {
        /*
            To run these tests, set the ot.SqlConnectionString machine-level environment variable to a valid Sql Server connection string.

            To use Docker...
             1) Run: docker run -d --name sql2019 -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=Pass@word" -p 5433:1433 mcr.microsoft.com/mssql/server:2019-latest
             2) Set ot.SqlConnectionString as: Data Source=127.0.0.1,5433; User ID=sa; Password=Pass@word
         */

        private const string SqlConnectionStringEnvVarName = "ot.SqlConnectionString";
        private static readonly string SqlConnectionString = Environment.GetEnvironmentVariable(SqlConnectionStringEnvVarName, EnvironmentVariableTarget.Machine);

        [Trait("CategoryName", "SqlIntegrationTests")]
        [SkipUnlessEnvVarFoundTheory(SqlConnectionStringEnvVarName)]
        [InlineData(CommandType.Text, "select 1/1", false)]
        [InlineData(CommandType.Text, "select 1/0", false, true)]
        [InlineData(CommandType.StoredProcedure, "sp_who", false)]
        [InlineData(CommandType.StoredProcedure, "sp_who", true)]
        public async Task SuccessfulCommandTest(CommandType commandType, string commandText, bool captureText, bool isFailure = false)
        {
            var activityProcessor = new Mock<ActivityProcessor>();
            using var shutdownSignal = OpenTelemetrySdk.EnableOpenTelemetry(b =>
            {
                b.AddProcessorPipeline(c => c.AddProcessor(ap => activityProcessor.Object));
                b.AddSqlClientDependencyInstrumentation(options =>
                {
                    options.CaptureStoredProcedureCommandName = captureText;
                });
            });

            string dataSource = null;

            using (var activityListener = new ActivityListener
            {
                ShouldListenTo = (activitySource) => activitySource.Name == SqlEventSourceListener.ActivitySourceName,
            })
            {
                ActivitySource.AddActivityListener(activityListener);

                using SqlConnection sqlConnection = new SqlConnection(SqlConnectionString);

                await sqlConnection.OpenAsync().ConfigureAwait(false);

                dataSource = sqlConnection.DataSource;

                sqlConnection.ChangeDatabase("master");

                using SqlCommand sqlCommand = new SqlCommand(commandText, sqlConnection)
                {
                    CommandType = commandType,
                };

                try
                {
                    await sqlCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
                catch
                {
                }
            }

            Assert.Equal(2, activityProcessor.Invocations.Count);

            var activity = (Activity)activityProcessor.Invocations[1].Arguments[0];

            Assert.Equal("master", activity.DisplayName);
            Assert.Equal(ActivityKind.Client, activity.Kind);
            Assert.Equal("sql", activity.Tags.FirstOrDefault(t => t.Key == SpanAttributeConstants.ComponentKey).Value);
            Assert.Equal(SqlClientDiagnosticListener.MicrosoftSqlServerDatabaseSystemName, activity.Tags.FirstOrDefault(t => t.Key == SpanAttributeConstants.DatabaseSystemKey).Value);
            Assert.Equal(dataSource, activity.Tags.FirstOrDefault(t => t.Key == SpanAttributeConstants.PeerServiceKey).Value);
            Assert.Equal("master", activity.Tags.FirstOrDefault(t => t.Key == SpanAttributeConstants.DatabaseNameKey).Value);
            Assert.Equal(commandType.ToString(), activity.Tags.FirstOrDefault(t => t.Key == SpanAttributeConstants.DatabaseStatementTypeKey).Value);
            if (commandType == CommandType.StoredProcedure)
            {
                if (captureText)
                {
                    Assert.Equal(commandText, activity.Tags.FirstOrDefault(t => t.Key == SpanAttributeConstants.DatabaseStatementKey).Value);
                }
                else
                {
                    Assert.DoesNotContain(activity.Tags, t => t.Key == SpanAttributeConstants.DatabaseStatementKey);
                }
            }

            if (!isFailure)
            {
                Assert.Equal("Ok", activity.Tags.FirstOrDefault(t => t.Key == SpanAttributeConstants.StatusCodeKey).Value);
            }
            else
            {
                Assert.Equal("Unknown", activity.Tags.FirstOrDefault(t => t.Key == SpanAttributeConstants.StatusCodeKey).Value);
                Assert.Contains(activity.Tags, t => t.Key == SpanAttributeConstants.StatusDescriptionKey);
            }
        }
    }
}
#endif
