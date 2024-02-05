// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using OpenTelemetry.Instrumentation.SqlClient.Implementation;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.SqlClient.Tests;

public class SqlEventSourceTests
{
    /*
        To run the integration tests, set the OTEL_SQLCONNECTIONSTRING machine-level environment variable to a valid Sql Server connection string.

        To use Docker...
         1) Run: docker run -d --name sql2019 -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=Pass@word" -p 5433:1433 mcr.microsoft.com/mssql/server:2019-latest
         2) Set OTEL_SQLCONNECTIONSTRING as: Data Source=127.0.0.1,5433; User ID=sa; Password=Pass@word
     */

    private const string SqlConnectionStringEnvVarName = "OTEL_SQLCONNECTIONSTRING";
    private static readonly string SqlConnectionString = SkipUnlessEnvVarFoundTheoryAttribute.GetEnvironmentVariable(SqlConnectionStringEnvVarName);

    [Trait("CategoryName", "SqlIntegrationTests")]
    [SkipUnlessEnvVarFoundTheory(SqlConnectionStringEnvVarName)]
    [InlineData(CommandType.Text, "select 1/1", false)]
    [InlineData(CommandType.Text, "select 1/0", false, true)]
    [InlineData(CommandType.StoredProcedure, "sp_who", false)]
    [InlineData(CommandType.StoredProcedure, "sp_who", true)]
    public async Task SuccessfulCommandTest(CommandType commandType, string commandText, bool captureText, bool isFailure = false)
    {
        var exportedItems = new List<Activity>();
        using var shutdownSignal = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(exportedItems)
            .AddSqlClientInstrumentation(options =>
            {
                options.SetDbStatementForText = captureText;
            })
            .Build();

        using SqlConnection sqlConnection = new SqlConnection(SqlConnectionString);

        await sqlConnection.OpenAsync();

        string dataSource = sqlConnection.DataSource;

        sqlConnection.ChangeDatabase("master");

        using SqlCommand sqlCommand = new SqlCommand(commandText, sqlConnection)
        {
            CommandType = commandType,
        };

        try
        {
            await sqlCommand.ExecuteNonQueryAsync();
        }
        catch
        {
        }

        Assert.Single(exportedItems);
        var activity = exportedItems[0];

        VerifyActivityData(commandText, captureText, isFailure, dataSource, activity);
    }

    [Theory]
    [InlineData(typeof(FakeBehavingAdoNetSqlEventSource), CommandType.Text, "select 1/1", false)]
    [InlineData(typeof(FakeBehavingAdoNetSqlEventSource), CommandType.Text, "select 1/0", false, true)]
    [InlineData(typeof(FakeBehavingAdoNetSqlEventSource), CommandType.StoredProcedure, "sp_who", false)]
    [InlineData(typeof(FakeBehavingAdoNetSqlEventSource), CommandType.StoredProcedure, "sp_who", true, false, 0, true)]
    [InlineData(typeof(FakeBehavingMdsSqlEventSource), CommandType.Text, "select 1/1", false)]
    [InlineData(typeof(FakeBehavingMdsSqlEventSource), CommandType.Text, "select 1/0", false, true)]
    [InlineData(typeof(FakeBehavingMdsSqlEventSource), CommandType.StoredProcedure, "sp_who", false)]
    [InlineData(typeof(FakeBehavingMdsSqlEventSource), CommandType.StoredProcedure, "sp_who", true, false, 0, true)]
    public void EventSourceFakeTests(
        Type eventSourceType,
        CommandType commandType,
        string commandText,
        bool captureText,
        bool isFailure = false,
        int sqlExceptionNumber = 0,
        bool enableConnectionLevelAttributes = false)
    {
        using IFakeBehavingSqlEventSource fakeSqlEventSource = (IFakeBehavingSqlEventSource)Activator.CreateInstance(eventSourceType);

        var exportedItems = new List<Activity>();
        using var shutdownSignal = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(exportedItems)
            .AddSqlClientInstrumentation(options =>
            {
                options.SetDbStatementForText = captureText;
                options.EnableConnectionLevelAttributes = enableConnectionLevelAttributes;
            })
            .Build();

        int objectId = Guid.NewGuid().GetHashCode();

        fakeSqlEventSource.WriteBeginExecuteEvent(objectId, "127.0.0.1", "master", commandType == CommandType.StoredProcedure ? commandText : string.Empty);

        // success is stored in the first bit in compositeState 0b001
        int successFlag = !isFailure ? 1 : 0;

        // isSqlException is stored in the second bit in compositeState 0b010
        int isSqlExceptionFlag = sqlExceptionNumber > 0 ? 2 : 0;

        // synchronous state is stored in the third bit in compositeState 0b100
        int synchronousFlag = false ? 4 : 0;

        int compositeState = successFlag | isSqlExceptionFlag | synchronousFlag;

        fakeSqlEventSource.WriteEndExecuteEvent(objectId, compositeState, sqlExceptionNumber);
        shutdownSignal.Dispose();
        Assert.Single(exportedItems);

        var activity = exportedItems[0];

        VerifyActivityData(commandText, captureText, isFailure, "127.0.0.1", activity, enableConnectionLevelAttributes);
    }

    [Theory]
    [InlineData(typeof(FakeMisbehavingAdoNetSqlEventSource))]
    [InlineData(typeof(FakeMisbehavingMdsSqlEventSource))]
    public void EventSourceFakeUnknownEventWithNullPayloadTest(Type eventSourceType)
    {
        using IFakeMisbehavingSqlEventSource fakeSqlEventSource = (IFakeMisbehavingSqlEventSource)Activator.CreateInstance(eventSourceType);

        var exportedItems = new List<Activity>();
        using var shutdownSignal = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(exportedItems)
            .AddSqlClientInstrumentation()
            .Build();

        fakeSqlEventSource.WriteUnknownEventWithNullPayload();

        shutdownSignal.Dispose();

        Assert.Empty(exportedItems);
    }

    [Theory]
    [InlineData(typeof(FakeMisbehavingAdoNetSqlEventSource))]
    [InlineData(typeof(FakeMisbehavingMdsSqlEventSource))]
    public void EventSourceFakeInvalidPayloadTest(Type eventSourceType)
    {
        using IFakeMisbehavingSqlEventSource fakeSqlEventSource = (IFakeMisbehavingSqlEventSource)Activator.CreateInstance(eventSourceType);

        var exportedItems = new List<Activity>();
        using var shutdownSignal = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(exportedItems)
            .AddSqlClientInstrumentation()
            .Build();

        fakeSqlEventSource.WriteBeginExecuteEvent("arg1");

        fakeSqlEventSource.WriteEndExecuteEvent("arg1", "arg2", "arg3", "arg4");
        shutdownSignal.Dispose();

        Assert.Empty(exportedItems);
    }

    [Theory]
    [InlineData(typeof(FakeBehavingAdoNetSqlEventSource))]
    [InlineData(typeof(FakeBehavingMdsSqlEventSource))]
    public void DefaultCaptureTextFalse(Type eventSourceType)
    {
        using IFakeBehavingSqlEventSource fakeSqlEventSource = (IFakeBehavingSqlEventSource)Activator.CreateInstance(eventSourceType);

        var exportedItems = new List<Activity>();
        using var shutdownSignal = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(exportedItems)
            .AddSqlClientInstrumentation()
            .Build();

        int objectId = Guid.NewGuid().GetHashCode();

        const string commandText = "TestCommandTest";
        fakeSqlEventSource.WriteBeginExecuteEvent(objectId, "127.0.0.1", "master", commandText);

        // success is stored in the first bit in compositeState 0b001
        int successFlag = 1;

        // isSqlException is stored in the second bit in compositeState 0b010
        int isSqlExceptionFlag = 2;

        // synchronous state is stored in the third bit in compositeState 0b100
        int synchronousFlag = 4;

        int compositeState = successFlag | isSqlExceptionFlag | synchronousFlag;

        fakeSqlEventSource.WriteEndExecuteEvent(objectId, compositeState, 0);
        shutdownSignal.Dispose();
        Assert.Single(exportedItems);

        var activity = exportedItems[0];

        const bool captureText = false;
        VerifyActivityData(commandText, captureText, false, "127.0.0.1", activity, false);
    }

    private static void VerifyActivityData(
        string commandText,
        bool captureText,
        bool isFailure,
        string dataSource,
        Activity activity,
        bool enableConnectionLevelAttributes = false)
    {
        Assert.Equal("master", activity.DisplayName);
        Assert.Equal(ActivityKind.Client, activity.Kind);
        Assert.Equal(SqlActivitySourceHelper.MicrosoftSqlServerDatabaseSystemName, activity.GetTagValue(SemanticConventions.AttributeDbSystem));

        if (!enableConnectionLevelAttributes)
        {
            Assert.Equal(dataSource, activity.GetTagValue(SemanticConventions.AttributePeerService));
        }
        else
        {
            var connectionDetails = SqlClientTraceInstrumentationOptions.ParseDataSource(dataSource);

            if (!string.IsNullOrEmpty(connectionDetails.ServerHostName))
            {
                Assert.Equal(connectionDetails.ServerHostName, activity.GetTagValue(SemanticConventions.AttributeNetPeerName));
            }
            else
            {
                Assert.Equal(connectionDetails.ServerIpAddress, activity.GetTagValue(SemanticConventions.AttributeServerSocketAddress));
            }

            if (!string.IsNullOrEmpty(connectionDetails.InstanceName))
            {
                Assert.Equal(connectionDetails.InstanceName, activity.GetTagValue(SemanticConventions.AttributeDbMsSqlInstanceName));
            }

            if (!string.IsNullOrEmpty(connectionDetails.Port))
            {
                Assert.Equal(connectionDetails.Port, activity.GetTagValue(SemanticConventions.AttributeNetPeerPort));
            }
        }

        Assert.Equal("master", activity.GetTagValue(SemanticConventions.AttributeDbName));

        // "db.statement_type" is never set by the SqlEventSource instrumentation
        Assert.Null(activity.GetTagValue(SpanAttributeConstants.DatabaseStatementTypeKey));
        if (captureText)
        {
            Assert.Equal(commandText, activity.GetTagValue(SemanticConventions.AttributeDbStatement));
        }
        else
        {
            Assert.Null(activity.GetTagValue(SemanticConventions.AttributeDbStatement));
        }

        if (!isFailure)
        {
            Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        }
        else
        {
            Assert.Equal(ActivityStatusCode.Error, activity.Status);
            Assert.NotNull(activity.StatusDescription);
        }
    }

#pragma warning disable SA1201 // Elements should appear in the correct order

    // Helper interface to be able to have single test method for multiple EventSources, want to keep it close to the event sources themselves.
    private interface IFakeBehavingSqlEventSource : IDisposable
#pragma warning restore SA1201 // Elements should appear in the correct order
    {
        void WriteBeginExecuteEvent(int objectId, string dataSource, string databaseName, string commandText);

        void WriteEndExecuteEvent(int objectId, int compositeState, int sqlExceptionNumber);
    }

    private interface IFakeMisbehavingSqlEventSource : IDisposable
    {
        void WriteBeginExecuteEvent(string arg1);

        void WriteEndExecuteEvent(string arg1, string arg2, string arg3, string arg4);

        void WriteUnknownEventWithNullPayload();
    }

    [EventSource(Name = SqlEventSourceListener.AdoNetEventSourceName + "-FakeFriendly")]
    private class FakeBehavingAdoNetSqlEventSource : EventSource, IFakeBehavingSqlEventSource
    {
        [Event(SqlEventSourceListener.BeginExecuteEventId)]
        public void WriteBeginExecuteEvent(int objectId, string dataSource, string databaseName, string commandText)
        {
            this.WriteEvent(SqlEventSourceListener.BeginExecuteEventId, objectId, dataSource, databaseName, commandText);
        }

        [Event(SqlEventSourceListener.EndExecuteEventId)]
        public void WriteEndExecuteEvent(int objectId, int compositeState, int sqlExceptionNumber)
        {
            this.WriteEvent(SqlEventSourceListener.EndExecuteEventId, objectId, compositeState, sqlExceptionNumber);
        }
    }

    [EventSource(Name = SqlEventSourceListener.MdsEventSourceName + "-FakeFriendly")]
    private class FakeBehavingMdsSqlEventSource : EventSource, IFakeBehavingSqlEventSource
    {
        [Event(SqlEventSourceListener.BeginExecuteEventId)]
        public void WriteBeginExecuteEvent(int objectId, string dataSource, string databaseName, string commandText)
        {
            this.WriteEvent(SqlEventSourceListener.BeginExecuteEventId, objectId, dataSource, databaseName, commandText);
        }

        [Event(SqlEventSourceListener.EndExecuteEventId)]
        public void WriteEndExecuteEvent(int objectId, int compositeState, int sqlExceptionNumber)
        {
            this.WriteEvent(SqlEventSourceListener.EndExecuteEventId, objectId, compositeState, sqlExceptionNumber);
        }
    }

    [EventSource(Name = SqlEventSourceListener.AdoNetEventSourceName + "-FakeEvil")]
    private class FakeMisbehavingAdoNetSqlEventSource : EventSource, IFakeMisbehavingSqlEventSource
    {
        [Event(SqlEventSourceListener.BeginExecuteEventId)]
        public void WriteBeginExecuteEvent(string arg1)
        {
            this.WriteEvent(SqlEventSourceListener.BeginExecuteEventId, arg1);
        }

        [Event(SqlEventSourceListener.EndExecuteEventId)]
        public void WriteEndExecuteEvent(string arg1, string arg2, string arg3, string arg4)
        {
            this.WriteEvent(SqlEventSourceListener.EndExecuteEventId, arg1, arg2, arg3, arg4);
        }

        [Event(3)]
        public void WriteUnknownEventWithNullPayload()
        {
            object[] args = null;

            this.WriteEvent(3, args);
        }
    }

    [EventSource(Name = SqlEventSourceListener.MdsEventSourceName + "-FakeEvil")]
    private class FakeMisbehavingMdsSqlEventSource : EventSource, IFakeMisbehavingSqlEventSource
    {
        [Event(SqlEventSourceListener.BeginExecuteEventId)]
        public void WriteBeginExecuteEvent(string arg1)
        {
            this.WriteEvent(SqlEventSourceListener.BeginExecuteEventId, arg1);
        }

        [Event(SqlEventSourceListener.EndExecuteEventId)]
        public void WriteEndExecuteEvent(string arg1, string arg2, string arg3, string arg4)
        {
            this.WriteEvent(SqlEventSourceListener.EndExecuteEventId, arg1, arg2, arg3, arg4);
        }

        [Event(3)]
        public void WriteUnknownEventWithNullPayload()
        {
            object[] args = null;

            this.WriteEvent(3, args);
        }
    }
}
#endif
