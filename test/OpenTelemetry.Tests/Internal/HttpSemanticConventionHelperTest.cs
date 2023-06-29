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

namespace OpenTelemetry.Internal.Tests;

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
        this.RunTestWithEnvironmentVariable(null, HttpSemanticConvention.Old);
        this.RunTestWithEnvironmentVariable(string.Empty, HttpSemanticConvention.Old);
        this.RunTestWithEnvironmentVariable("junk", HttpSemanticConvention.Old);
        this.RunTestWithEnvironmentVariable("none", HttpSemanticConvention.Old);
        this.RunTestWithEnvironmentVariable("NONE", HttpSemanticConvention.Old);
        this.RunTestWithEnvironmentVariable("http", HttpSemanticConvention.New);
        this.RunTestWithEnvironmentVariable("HTTP", HttpSemanticConvention.New);
        this.RunTestWithEnvironmentVariable("http/dup", HttpSemanticConvention.Dupe);
        this.RunTestWithEnvironmentVariable("HTTP/DUP", HttpSemanticConvention.Dupe);
    }

    [Fact]
    public void VerifyGetSemanticConventionOptInUsingIConfiguration()
    {
        this.RunTestWithIConfiguration(null, HttpSemanticConvention.Old);
        this.RunTestWithIConfiguration(string.Empty, HttpSemanticConvention.Old);
        this.RunTestWithIConfiguration("junk", HttpSemanticConvention.Old);
        this.RunTestWithIConfiguration("none", HttpSemanticConvention.Old);
        this.RunTestWithIConfiguration("NONE", HttpSemanticConvention.Old);
        this.RunTestWithIConfiguration("http", HttpSemanticConvention.New);
        this.RunTestWithIConfiguration("HTTP", HttpSemanticConvention.New);
        this.RunTestWithIConfiguration("http/dup", HttpSemanticConvention.Dupe);
        this.RunTestWithIConfiguration("HTTP/DUP", HttpSemanticConvention.Dupe);
    }

    private void RunTestWithEnvironmentVariable(string value, HttpSemanticConvention expected)
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

    private void RunTestWithIConfiguration(string value, HttpSemanticConvention expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { [SemanticConventionOptInKeyName] = value })
            .Build();

        Assert.Equal(expected, GetSemanticConventionOptIn(configuration));
    }
}
