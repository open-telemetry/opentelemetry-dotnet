// <copyright file="SqlClientTests.cs" company="OpenTelemetry Authors">
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
using System.Data;
using System.Diagnostics;
using System.Linq;
using Microsoft.Data.SqlClient;
using Moq;
using OpenTelemetry.Instrumentation.SqlClient.Implementation;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.SqlClient.Tests
{
    public class SqlClientTests : IDisposable
    {
        /*
            To run the integration tests, set the OTEL_SQLCONNECTIONSTRING machine-level environment variable to a valid Sql Server connection string.

            To use Docker...
             1) Run: docker run -d --name sql2019 -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=Pass@word" -p 5433:1433 mcr.microsoft.com/mssql/server:2019-latest
             2) Set OTEL_SQLCONNECTIONSTRING as: Data Source=127.0.0.1,5433; User ID=sa; Password=Pass@word
         */

        private const string SqlConnectionStringEnvVarName = "OTEL_SQLCONNECTIONSTRING";
        private const string TestConnectionString = "Data Source=(localdb)\\MSSQLLocalDB;Database=master";

        private static readonly string SqlConnectionString = SkipUnlessEnvVarFoundTheoryAttribute.GetEnvironmentVariable(SqlConnectionStringEnvVarName);

        private readonly FakeSqlClientDiagnosticSource fakeSqlClientDiagnosticSource;

        public SqlClientTests()
        {
            this.fakeSqlClientDiagnosticSource = new FakeSqlClientDiagnosticSource();
        }

        public void Dispose()
        {
            this.fakeSqlClientDiagnosticSource.Dispose();
        }

        [Fact]
        public void SqlClient_BadArgs()
        {
            TracerProviderBuilder builder = null;
            Assert.Throws<ArgumentNullException>(() => builder.AddSqlClientInstrumentation());
        }

        [Trait("CategoryName", "SqlIntegrationTests")]
        [SkipUnlessEnvVarFoundTheory(SqlConnectionStringEnvVarName)]
        [InlineData(CommandType.Text, "select 1/1", false)]
#if !NETFRAMEWORK
        [InlineData(CommandType.Text, "select 1/1", false, true)]
#endif
        [InlineData(CommandType.Text, "select 1/0", false, false, true)]
        [InlineData(CommandType.Text, "select 1/0", false, false, true, false, false)]
        [InlineData(CommandType.Text, "select 1/0", false, false, true, true, false)]
        [InlineData(CommandType.StoredProcedure, "sp_who", false)]
        [InlineData(CommandType.StoredProcedure, "sp_who", true)]
        public void SuccessfulCommandTest(
            CommandType commandType,
            string commandText,
            bool captureStoredProcedureCommandName,
            bool captureTextCommandContent = false,
            bool isFailure = false,
            bool recordException = false,
            bool shouldEnrich = true)
        {
            var activityProcessor = new Mock<BaseProcessor<Activity>>();
            activityProcessor.Setup(x => x.OnStart(It.IsAny<Activity>())).Callback<Activity>(c => c.SetTag("enriched", "no"));
            var sampler = new TestSampler();
            using var shutdownSignal = Sdk.CreateTracerProviderBuilder()
                .AddProcessor(activityProcessor.Object)
                .SetSampler(sampler)
                .AddSqlClientInstrumentation(options =>
                {
#if !NETFRAMEWORK
                    options.SetDbStatementForStoredProcedure = captureStoredProcedureCommandName;
                    options.SetDbStatementForText = captureTextCommandContent;
                    options.RecordException = recordException;
#else
                    options.SetDbStatement = captureStoredProcedureCommandName;
#endif
                    if (shouldEnrich)
                    {
                        options.Enrich = ActivityEnrichment;
                    }
                })
                .Build();

#if NETFRAMEWORK
            // RecordException not available on netfx
            recordException = false;
#endif

            using SqlConnection sqlConnection = new SqlConnection(SqlConnectionString);

            sqlConnection.Open();

            string dataSource = sqlConnection.DataSource;

            sqlConnection.ChangeDatabase("master");

            using SqlCommand sqlCommand = new SqlCommand(commandText, sqlConnection)
            {
                CommandType = commandType,
            };

            try
            {
                sqlCommand.ExecuteNonQuery();
            }
            catch
            {
            }

            Assert.Equal(3, activityProcessor.Invocations.Count);

            var activity = (Activity)activityProcessor.Invocations[1].Arguments[0];

            VerifyActivityData(commandType, commandText, captureStoredProcedureCommandName, captureTextCommandContent, isFailure, recordException, shouldEnrich, dataSource, activity);
            VerifySamplingParameters(sampler.LatestSamplingParameters);
        }

        // DiagnosticListener-based instrumentation is only available on .NET Core
#if !NETFRAMEWORK
        [Theory]
        [InlineData(SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, SqlClientDiagnosticListener.SqlDataAfterExecuteCommand, CommandType.StoredProcedure, "SP_GetOrders", true, false)]
        [InlineData(SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, SqlClientDiagnosticListener.SqlDataAfterExecuteCommand, CommandType.StoredProcedure, "SP_GetOrders", true, false, false)]
        [InlineData(SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, SqlClientDiagnosticListener.SqlDataAfterExecuteCommand, CommandType.Text, "select * from sys.databases", true, false)]
        [InlineData(SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, SqlClientDiagnosticListener.SqlDataAfterExecuteCommand, CommandType.Text, "select * from sys.databases", true, false, false)]
        [InlineData(SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand, SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand, CommandType.StoredProcedure, "SP_GetOrders", false, true)]
        [InlineData(SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand, SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand, CommandType.StoredProcedure, "SP_GetOrders", false, true, false)]
        [InlineData(SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand, SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand, CommandType.Text, "select * from sys.databases", false, true)]
        [InlineData(SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand, SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand, CommandType.Text, "select * from sys.databases", false, true, false)]
        public void SqlClientCallsAreCollectedSuccessfully(
            string beforeCommand,
            string afterCommand,
            CommandType commandType,
            string commandText,
            bool captureStoredProcedureCommandName,
            bool captureTextCommandContent,
            bool shouldEnrich = true)
        {
            using var sqlConnection = new SqlConnection(TestConnectionString);
            using var sqlCommand = sqlConnection.CreateCommand();

            var processor = new Mock<BaseProcessor<Activity>>();
            processor.Setup(x => x.OnStart(It.IsAny<Activity>())).Callback<Activity>(c => c.SetTag("enriched", "no"));
            using (Sdk.CreateTracerProviderBuilder()
                    .AddSqlClientInstrumentation(
                        (opt) =>
                        {
                            opt.SetDbStatementForText = captureTextCommandContent;
                            opt.SetDbStatementForStoredProcedure = captureStoredProcedureCommandName;
                            if (shouldEnrich)
                            {
                                opt.Enrich = ActivityEnrichment;
                            }
                        })
                    .AddProcessor(processor.Object)
                    .Build())
            {
                var operationId = Guid.NewGuid();
                sqlCommand.CommandType = commandType;
                sqlCommand.CommandText = commandText;

                var beforeExecuteEventData = new
                {
                    OperationId = operationId,
                    Command = sqlCommand,
                    Timestamp = (long?)1000000L,
                };

                this.fakeSqlClientDiagnosticSource.Write(
                    beforeCommand,
                    beforeExecuteEventData);

                var afterExecuteEventData = new
                {
                    OperationId = operationId,
                    Command = sqlCommand,
                    Timestamp = 2000000L,
                };

                this.fakeSqlClientDiagnosticSource.Write(
                    afterCommand,
                    afterExecuteEventData);
            }

            Assert.Equal(5, processor.Invocations.Count); // SetParentProvider/OnStart/OnEnd/OnShutdown/Dispose called.

            VerifyActivityData(
                sqlCommand.CommandType,
                sqlCommand.CommandText,
                captureStoredProcedureCommandName,
                captureTextCommandContent,
                false,
                false,
                shouldEnrich,
                sqlConnection.DataSource,
                (Activity)processor.Invocations[2].Arguments[0]);
        }

        [Theory]
        [InlineData(SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, SqlClientDiagnosticListener.SqlDataWriteCommandError)]
        [InlineData(SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, SqlClientDiagnosticListener.SqlDataWriteCommandError, false)]
        [InlineData(SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, SqlClientDiagnosticListener.SqlDataWriteCommandError, false, true)]
        [InlineData(SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand, SqlClientDiagnosticListener.SqlMicrosoftWriteCommandError)]
        [InlineData(SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand, SqlClientDiagnosticListener.SqlMicrosoftWriteCommandError, false)]
        [InlineData(SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand, SqlClientDiagnosticListener.SqlMicrosoftWriteCommandError, false, true)]
        public void SqlClientErrorsAreCollectedSuccessfully(string beforeCommand, string errorCommand, bool shouldEnrich = true, bool recordException = false)
        {
            using var sqlConnection = new SqlConnection(TestConnectionString);
            using var sqlCommand = sqlConnection.CreateCommand();

            var processor = new Mock<BaseProcessor<Activity>>();
            processor.Setup(x => x.OnStart(It.IsAny<Activity>())).Callback<Activity>(c => c.SetTag("enriched", "no"));
            using (Sdk.CreateTracerProviderBuilder()
                .AddSqlClientInstrumentation(options =>
                {
                    options.RecordException = recordException;
                    if (shouldEnrich)
                    {
                        options.Enrich = ActivityEnrichment;
                    }
                })
                .AddProcessor(processor.Object)
                .Build())
            {
                var operationId = Guid.NewGuid();
                sqlCommand.CommandText = "SP_GetOrders";
                sqlCommand.CommandType = CommandType.StoredProcedure;

                var beforeExecuteEventData = new
                {
                    OperationId = operationId,
                    Command = sqlCommand,
                    Timestamp = (long?)1000000L,
                };

                this.fakeSqlClientDiagnosticSource.Write(
                    beforeCommand,
                    beforeExecuteEventData);

                var commandErrorEventData = new
                {
                    OperationId = operationId,
                    Command = sqlCommand,
                    Exception = new Exception("Boom!"),
                    Timestamp = 2000000L,
                };

                this.fakeSqlClientDiagnosticSource.Write(
                    errorCommand,
                    commandErrorEventData);
            }

            Assert.Equal(5, processor.Invocations.Count); // SetParentProvider/OnStart/OnEnd/OnShutdown/Dispose called.

            VerifyActivityData(
                sqlCommand.CommandType,
                sqlCommand.CommandText,
                true,
                false,
                true,
                recordException,
                shouldEnrich,
                sqlConnection.DataSource,
                (Activity)processor.Invocations[2].Arguments[0]);
        }

        [Theory]
        [InlineData(SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand)]
        [InlineData(SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand)]
        public void SqlClientCreatesActivityWithDbSystem(
            string beforeCommand)
        {
            using var sqlConnection = new SqlConnection(TestConnectionString);
            using var sqlCommand = sqlConnection.CreateCommand();

            var sampler = new TestSampler
            {
                SamplingAction = _ => new SamplingResult(SamplingDecision.Drop),
            };
            using (Sdk.CreateTracerProviderBuilder()
                .AddSqlClientInstrumentation()
                .SetSampler(sampler)
                .Build())
            {
                this.fakeSqlClientDiagnosticSource.Write(beforeCommand, new { });
            }

            VerifySamplingParameters(sampler.LatestSamplingParameters);
        }
#endif

        private static void VerifyActivityData(
            CommandType commandType,
            string commandText,
            bool captureStoredProcedureCommandName,
            bool captureTextCommandContent,
            bool isFailure,
            bool recordException,
            bool shouldEnrich,
            string dataSource,
            Activity activity)
        {
            Assert.Equal("master", activity.DisplayName);
            Assert.Equal(ActivityKind.Client, activity.Kind);

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

            Assert.NotEmpty(activity.Tags.Where(tag => tag.Key == "enriched"));
            Assert.Equal(shouldEnrich ? "yes" : "no", activity.Tags.Where(tag => tag.Key == "enriched").FirstOrDefault().Value);

            Assert.Equal(SqlActivitySourceHelper.MicrosoftSqlServerDatabaseSystemName, activity.GetTagValue(SemanticConventions.AttributeDbSystem));
            Assert.Equal("master", activity.GetTagValue(SemanticConventions.AttributeDbName));

            switch (commandType)
            {
                case CommandType.StoredProcedure:
                    if (captureStoredProcedureCommandName)
                    {
                        Assert.Equal(commandText, activity.GetTagValue(SemanticConventions.AttributeDbStatement));
                    }
                    else
                    {
                        Assert.Null(activity.GetTagValue(SemanticConventions.AttributeDbStatement));
                    }

                    break;

                case CommandType.Text:
                    if (captureTextCommandContent)
                    {
                        Assert.Equal(commandText, activity.GetTagValue(SemanticConventions.AttributeDbStatement));
                    }
                    else
                    {
                        Assert.Null(activity.GetTagValue(SemanticConventions.AttributeDbStatement));
                    }

                    break;
            }

            Assert.Equal(dataSource, activity.GetTagValue(SemanticConventions.AttributePeerService));
        }

        private static void VerifySamplingParameters(SamplingParameters samplingParameters)
        {
            Assert.NotNull(samplingParameters.Tags);
            Assert.Contains(
                samplingParameters.Tags,
                kvp => kvp.Key == SemanticConventions.AttributeDbSystem
                       && (string)kvp.Value == SqlActivitySourceHelper.MicrosoftSqlServerDatabaseSystemName);
        }

        private static void ActivityEnrichment(Activity activity, string method, object obj)
        {
            Assert.NotEmpty(activity.Tags.Where(tag => tag.Key == "enriched"));
            Assert.Equal("no", activity.Tags.Where(tag => tag.Key == "enriched").FirstOrDefault().Value);
            activity.SetTag("enriched", "yes");

            switch (method)
            {
                case "OnCustom":
                    Assert.True(obj is SqlCommand);
                    break;

                default:
                    break;
            }
        }

        private class FakeSqlClientDiagnosticSource : IDisposable
        {
            private readonly DiagnosticListener listener;

            public FakeSqlClientDiagnosticSource()
            {
                this.listener = new DiagnosticListener(SqlClientInstrumentation.SqlClientDiagnosticListenerName);
            }

            public void Write(string name, object value)
            {
                this.listener.Write(name, value);
            }

            public void Dispose()
            {
                this.listener.Dispose();
            }
        }
    }
}
