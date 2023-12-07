// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Trace;
using Xunit;
using static OpenTelemetry.Internal.HttpSemanticConventionHelper;

namespace OpenTelemetry.Instrumentation.SqlClient.Tests;

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
    [InlineData("tcp:127.0.0.1\\instanceName, 1818", null, "127.0.0.1", "instanceName", "1818")]
    [InlineData("tcp:localhost", "localhost", null, null, null)]
    [InlineData("tcp : localhost", "localhost", null, null, null)]
    [InlineData("np : localhost", "localhost", null, null, null)]
    [InlineData("lpc:localhost", "localhost", null, null, null)]
    [InlineData("np:\\\\localhost\\pipe\\sql\\query", "localhost", null, null, null)]
    [InlineData("np : \\\\localhost\\pipe\\sql\\query", "localhost", null, null, null)]
    [InlineData("np:\\\\localhost\\pipe\\MSSQL$instanceName\\sql\\query", "localhost", null, "instanceName", null)]
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
            Assert.Equal(expectedServerHostName, activity.GetTagValue(SemanticConventions.AttributePeerService));
        }
        else
        {
            Assert.Equal(expectedServerHostName, activity.GetTagValue(SemanticConventions.AttributeNetPeerName));
        }

        Assert.Equal(expectedServerIpAddress, activity.GetTagValue(SemanticConventions.AttributeNetPeerIp));
        Assert.Equal(expectedInstanceName, activity.GetTagValue(SemanticConventions.AttributeDbMsSqlInstanceName));
        Assert.Equal(expectedPort, activity.GetTagValue(SemanticConventions.AttributeNetPeerPort));
    }

    // Tests for v1.21.0 Semantic Conventions for database client calls.
    // see the spec https://github.com/open-telemetry/semantic-conventions/blob/v1.21.0/docs/database/database-spans.md
    // This test emits the new attributes.
    // This test method can replace the other (old) test method when this library is GA.
    [Theory]
    [InlineData(true, "localhost", "localhost", null, null, null)]
    [InlineData(true, "127.0.0.1,1433", null, "127.0.0.1", null, null)]
    [InlineData(true, "127.0.0.1,1434", null, "127.0.0.1", null, "1434")]
    [InlineData(true, "127.0.0.1\\instanceName, 1818", null, "127.0.0.1", "instanceName", "1818")]
    [InlineData(false, "localhost", "localhost", null, null, null)]
    public void SqlClientInstrumentationOptions_EnableConnectionLevelAttributes_New(
        bool enableConnectionLevelAttributes,
        string dataSource,
        string expectedServerHostName,
        string expectedServerIpAddress,
        string expectedInstanceName,
        string expectedPort)
    {
        var source = new ActivitySource("sql-client-instrumentation");
        var activity = source.StartActivity("Test Sql Activity");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { [SemanticConventionOptInKeyName] = "http" })
            .Build();

        var options = new SqlClientInstrumentationOptions(configuration)
        {
            EnableConnectionLevelAttributes = enableConnectionLevelAttributes,
        };
        options.AddConnectionLevelDetailsToActivity(dataSource, activity);

        if (!enableConnectionLevelAttributes)
        {
            Assert.Equal(expectedServerHostName, activity.GetTagValue(SemanticConventions.AttributePeerService));
        }
        else
        {
            Assert.Equal(expectedServerHostName, activity.GetTagValue(SemanticConventions.AttributeServerAddress));
        }

        Assert.Equal(expectedServerIpAddress, activity.GetTagValue(SemanticConventions.AttributeServerSocketAddress));
        Assert.Equal(expectedInstanceName, activity.GetTagValue(SemanticConventions.AttributeDbMsSqlInstanceName));
        Assert.Equal(expectedPort, activity.GetTagValue(SemanticConventions.AttributeServerPort));
    }

    // Tests for v1.21.0 Semantic Conventions for database client calls.
    // see the spec https://github.com/open-telemetry/semantic-conventions/blob/v1.21.0/docs/database/database-spans.md
    // This test emits both the new and older attributes.
    // This test method can be deleted when this library is GA.
    [Theory]
    [InlineData(true, "localhost", "localhost", null, null, null)]
    [InlineData(true, "127.0.0.1,1433", null, "127.0.0.1", null, null)]
    [InlineData(true, "127.0.0.1,1434", null, "127.0.0.1", null, "1434")]
    [InlineData(true, "127.0.0.1\\instanceName, 1818", null, "127.0.0.1", "instanceName", "1818")]
    [InlineData(false, "localhost", "localhost", null, null, null)]
    public void SqlClientInstrumentationOptions_EnableConnectionLevelAttributes_Dupe(
        bool enableConnectionLevelAttributes,
        string dataSource,
        string expectedServerHostName,
        string expectedServerIpAddress,
        string expectedInstanceName,
        string expectedPort)
    {
        var source = new ActivitySource("sql-client-instrumentation");
        var activity = source.StartActivity("Test Sql Activity");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { [SemanticConventionOptInKeyName] = "http/dup" })
            .Build();

        var options = new SqlClientInstrumentationOptions(configuration)
        {
            EnableConnectionLevelAttributes = enableConnectionLevelAttributes,
        };
        options.AddConnectionLevelDetailsToActivity(dataSource, activity);

        // New
        if (!enableConnectionLevelAttributes)
        {
            Assert.Equal(expectedServerHostName, activity.GetTagValue(SemanticConventions.AttributePeerService));
        }
        else
        {
            Assert.Equal(expectedServerHostName, activity.GetTagValue(SemanticConventions.AttributeServerAddress));
        }

        Assert.Equal(expectedServerIpAddress, activity.GetTagValue(SemanticConventions.AttributeServerSocketAddress));
        Assert.Equal(expectedInstanceName, activity.GetTagValue(SemanticConventions.AttributeDbMsSqlInstanceName));
        Assert.Equal(expectedPort, activity.GetTagValue(SemanticConventions.AttributeServerPort));

        // Old
        if (!enableConnectionLevelAttributes)
        {
            Assert.Equal(expectedServerHostName, activity.GetTagValue(SemanticConventions.AttributePeerService));
        }
        else
        {
            Assert.Equal(expectedServerHostName, activity.GetTagValue(SemanticConventions.AttributeNetPeerName));
        }

        Assert.Equal(expectedServerIpAddress, activity.GetTagValue(SemanticConventions.AttributeNetPeerIp));
        Assert.Equal(expectedInstanceName, activity.GetTagValue(SemanticConventions.AttributeDbMsSqlInstanceName));
        Assert.Equal(expectedPort, activity.GetTagValue(SemanticConventions.AttributeNetPeerPort));
    }
}
