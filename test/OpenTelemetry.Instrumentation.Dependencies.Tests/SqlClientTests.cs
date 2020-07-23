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
using System.Threading.Tasks;
#if NET452
using System.Data.SqlClient;
#else
using Microsoft.Data.SqlClient;
#endif
using Moq;
using OpenTelemetry.Instrumentation.Dependencies.Implementation;
using OpenTelemetry.Internal.Test;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Export;
using Xunit;

namespace OpenTelemetry.Instrumentation.Dependencies.Tests
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

        [Trait("CategoryName", "SqlIntegrationTests")]
        [SkipUnlessEnvVarFoundTheory(SqlConnectionStringEnvVarName)]
        [InlineData(CommandType.Text, "select 1/1", false)]
#if !NETFRAMEWORK
        [InlineData(CommandType.Text, "select 1/1", false, true)]
#endif
        [InlineData(CommandType.Text, "select 1/0", false, false, true)]
        [InlineData(CommandType.StoredProcedure, "sp_who", false)]
        [InlineData(CommandType.StoredProcedure, "sp_who", true)]
        public void SuccessfulCommandTest(
            CommandType commandType,
            string commandText,
            bool captureStoredProcedureCommandName,
            bool captureTextCommandContent = false,
            bool isFailure = false)
        {
            var activityProcessor = new Mock<ActivityProcessor>();
            using var shutdownSignal = TracerProviderSdk.EnableTracerProvider(b =>
            {
                b.AddProcessorPipeline(c => c.AddProcessor(ap => activityProcessor.Object));
                b.AddSqlClientDependencyInstrumentation(options =>
                {
                    options.SetStoredProcedureCommandName = captureStoredProcedureCommandName;
                    options.SetTextCommandContent = captureTextCommandContent;
                });
            });

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

            Assert.Equal(2, activityProcessor.Invocations.Count);

            var activity = (Activity)activityProcessor.Invocations[1].Arguments[0];

            VerifyActivityData(commandType, commandText, captureStoredProcedureCommandName, captureTextCommandContent, isFailure, dataSource, activity);
        }

        [Theory]
        [InlineData(SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, SqlClientDiagnosticListener.SqlDataAfterExecuteCommand, CommandType.StoredProcedure, "SP_GetOrders", true, false)]
        [InlineData(SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, SqlClientDiagnosticListener.SqlDataAfterExecuteCommand, CommandType.Text, "select * from sys.databases", true, false)]
        [InlineData(SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand, SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand, CommandType.StoredProcedure, "SP_GetOrders", false, true)]
        [InlineData(SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand, SqlClientDiagnosticListener.SqlMicrosoftAfterExecuteCommand, CommandType.Text, "select * from sys.databases", false, true)]
        public void SqlClientCallsAreCollectedSuccessfully(
            string beforeCommand,
            string afterCommand,
            CommandType commandType,
            string commandText,
            bool captureStoredProcedureCommandName,
            bool captureTextCommandContent)
        {
            using var sqlConnection = new SqlConnection(TestConnectionString);
            using var sqlCommand = sqlConnection.CreateCommand();

            var spanProcessor = new Mock<ActivityProcessor>();
            using (TracerProviderSdk.EnableTracerProvider(
                    (builder) => builder.AddSqlClientDependencyInstrumentation(
                        (opt) =>
                        {
                            opt.SetTextCommandContent = captureTextCommandContent;
                            opt.SetStoredProcedureCommandName = captureStoredProcedureCommandName;
                        })
                    .AddProcessorPipeline(p => p.AddProcessor(n => spanProcessor.Object))))
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

            Assert.Equal(2, spanProcessor.Invocations.Count); // begin and end was called

            VerifyActivityData(sqlCommand.CommandType, sqlCommand.CommandText, captureStoredProcedureCommandName, captureTextCommandContent, false, sqlConnection.DataSource, (Activity)spanProcessor.Invocations[1].Arguments[0]);
        }

        [Theory]
        [InlineData(SqlClientDiagnosticListener.SqlDataBeforeExecuteCommand, SqlClientDiagnosticListener.SqlDataWriteCommandError)]
        [InlineData(SqlClientDiagnosticListener.SqlMicrosoftBeforeExecuteCommand, SqlClientDiagnosticListener.SqlMicrosoftWriteCommandError)]
        public void SqlClientErrorsAreCollectedSuccessfully(string beforeCommand, string errorCommand)
        {
            using var sqlConnection = new SqlConnection(TestConnectionString);
            using var sqlCommand = sqlConnection.CreateCommand();

            var spanProcessor = new Mock<ActivityProcessor>();
            using (TracerProviderSdk.EnableTracerProvider(
                (builder) => builder.AddSqlClientDependencyInstrumentation()
                .AddProcessorPipeline(p => p.AddProcessor(n => spanProcessor.Object))))
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

            Assert.Equal(2, spanProcessor.Invocations.Count); // begin and end was called

            VerifyActivityData(sqlCommand.CommandType, sqlCommand.CommandText, true, false, true, sqlConnection.DataSource, (Activity)spanProcessor.Invocations[1].Arguments[0]);
        }

        private static void VerifyActivityData(
            CommandType commandType,
            string commandText,
            bool captureStoredProcedureCommandName,
            bool captureTextCommandContent,
            bool isFailure,
            string dataSource,
            Activity activity)
        {
            Assert.Equal("master", activity.DisplayName);
            Assert.Equal(ActivityKind.Client, activity.Kind);

            if (!isFailure)
            {
                Assert.Equal("Ok", activity.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.StatusCodeKey).Value);
                Assert.Null(activity.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.StatusDescriptionKey).Value);
            }
            else
            {
                Assert.Equal("Unknown", activity.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.StatusCodeKey).Value);
                Assert.Contains(activity.Tags, i => i.Key == SpanAttributeConstants.StatusDescriptionKey);
            }

            Assert.Equal(SqlClientDiagnosticListener.MicrosoftSqlServerDatabaseSystemName, activity.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeDbSystem).Value);
            Assert.Equal("master", activity.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeDbName).Value);

            switch (commandType)
            {
                case CommandType.StoredProcedure:
                    if (captureStoredProcedureCommandName)
                    {
                        Assert.Equal(commandText, activity.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeDbStatement).Value);
                    }
                    else
                    {
                        Assert.Null(activity.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeDbStatement).Value);
                    }

                    break;

                case CommandType.Text:
                    if (captureTextCommandContent)
                    {
                        Assert.Equal(commandText, activity.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeDbStatement).Value);
                    }
                    else
                    {
                        Assert.Null(activity.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributeDbStatement).Value);
                    }

                    break;
            }

            Assert.Equal(dataSource, activity.Tags.FirstOrDefault(i => i.Key == SemanticConventions.AttributePeerService).Value);
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
