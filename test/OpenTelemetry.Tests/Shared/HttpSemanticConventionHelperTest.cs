// <copyright file="HttpSemanticConventionHelperTest.cs" company="OpenTelemetry Authors">
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

using Microsoft.Extensions.Configuration;
using Xunit;
using static OpenTelemetry.Internal.HttpSemanticConventionHelper;

namespace OpenTelemetry.Tests.Shared;

public class HttpSemanticConventionHelperTest
{
    [Fact]
    public void VerifyFlags()
    {
        var testValue = HttpSemanticConvention.Dupe;
        Assert.True(testValue.HasFlag(HttpSemanticConvention.Old));
        Assert.True(testValue.HasFlag(HttpSemanticConvention.New));

        testValue = HttpSemanticConvention.Old;
        Assert.True(testValue.HasFlag(HttpSemanticConvention.Old));
        Assert.False(testValue.HasFlag(HttpSemanticConvention.New));

        testValue = HttpSemanticConvention.New;
        Assert.False(testValue.HasFlag(HttpSemanticConvention.Old));
        Assert.True(testValue.HasFlag(HttpSemanticConvention.New));
    }

    [Fact]
    public void VerifyGetSemanticConventionOptIn()
    {
        RunTestWithEnvironmentVariable(null, HttpSemanticConvention.Old);
        RunTestWithEnvironmentVariable(string.Empty, HttpSemanticConvention.Old);
        RunTestWithEnvironmentVariable("junk", HttpSemanticConvention.Old);
        RunTestWithEnvironmentVariable("none", HttpSemanticConvention.Old);
        RunTestWithEnvironmentVariable("NONE", HttpSemanticConvention.Old);
        RunTestWithEnvironmentVariable("http", HttpSemanticConvention.New);
        RunTestWithEnvironmentVariable("HTTP", HttpSemanticConvention.New);
        RunTestWithEnvironmentVariable("http/dup", HttpSemanticConvention.Dupe);
        RunTestWithEnvironmentVariable("HTTP/DUP", HttpSemanticConvention.Dupe);
    }

    [Fact]
    public void VerifyGetSemanticConventionOptInUsingIConfiguration()
    {
        RunTestWithIConfiguration(null, HttpSemanticConvention.Old);
        RunTestWithIConfiguration(string.Empty, HttpSemanticConvention.Old);
        RunTestWithIConfiguration("junk", HttpSemanticConvention.Old);
        RunTestWithIConfiguration("none", HttpSemanticConvention.Old);
        RunTestWithIConfiguration("NONE", HttpSemanticConvention.Old);
        RunTestWithIConfiguration("http", HttpSemanticConvention.New);
        RunTestWithIConfiguration("HTTP", HttpSemanticConvention.New);
        RunTestWithIConfiguration("http/dup", HttpSemanticConvention.Dupe);
        RunTestWithIConfiguration("HTTP/DUP", HttpSemanticConvention.Dupe);
    }

    private static void RunTestWithEnvironmentVariable(string value, HttpSemanticConvention expected)
    {
        try
        {
            Environment.SetEnvironmentVariable(SemanticConventionOptInKeyName, value);

            Assert.Equal(expected, GetSemanticConventionOptIn(new ConfigurationBuilder().AddEnvironmentVariables().Build()));
        }
        finally
        {
            Environment.SetEnvironmentVariable(SemanticConventionOptInKeyName, null);
        }
    }

    private static void RunTestWithIConfiguration(string value, HttpSemanticConvention expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { [SemanticConventionOptInKeyName] = value })
            .Build();

        Assert.Equal(expected, GetSemanticConventionOptIn(configuration));
    }
}
