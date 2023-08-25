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
    public static IEnumerable<object[]> TestCases => new List<object[]>
        {
            new object[] { null,  HttpSemanticConvention.Old },
            new object[] { string.Empty,  HttpSemanticConvention.Old },
            new object[] { " ",  HttpSemanticConvention.Old },
            new object[] { "junk",  HttpSemanticConvention.Old },
            new object[] { "none",  HttpSemanticConvention.Old },
            new object[] { "NONE",  HttpSemanticConvention.Old },
            new object[] { "http",  HttpSemanticConvention.New },
            new object[] { "HTTP",  HttpSemanticConvention.New },
            new object[] { "http/dup",  HttpSemanticConvention.Dupe },
            new object[] { "HTTP/DUP",  HttpSemanticConvention.Dupe },
            new object[] { "junk,,junk",  HttpSemanticConvention.Old },
            new object[] { "junk,JUNK",  HttpSemanticConvention.Old },
            new object[] { "junk1,junk2",  HttpSemanticConvention.Old },
            new object[] { "junk,http",  HttpSemanticConvention.New },
            new object[] { "junk,http , http ,junk",  HttpSemanticConvention.New },
            new object[] { "junk,http/dup",  HttpSemanticConvention.Dupe },
            new object[] { "junk, http/dup ",  HttpSemanticConvention.Dupe },
            new object[] { "http/dup,http",  HttpSemanticConvention.Dupe },
            new object[] { "http,http/dup",  HttpSemanticConvention.Dupe },
        };

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

    [Theory]
    [MemberData(nameof(TestCases))]
    public void VerifyGetSemanticConventionOptIn_UsingEnvironmentVariable(string input, string expectedValue)
    {
        try
        {
            Environment.SetEnvironmentVariable(SemanticConventionOptInKeyName, input);

            var expected = Enum.Parse(typeof(HttpSemanticConvention), expectedValue);
            Assert.Equal(expected, GetSemanticConventionOptIn(new ConfigurationBuilder().AddEnvironmentVariables().Build()));
        }
        finally
        {
            Environment.SetEnvironmentVariable(SemanticConventionOptInKeyName, null);
        }
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public void VerifyGetSemanticConventionOptIn_UsingIConfiguration(string input, string expectedValue)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { [SemanticConventionOptInKeyName] = input })
            .Build();

        var expected = Enum.Parse(typeof(HttpSemanticConvention), expectedValue);
        Assert.Equal(expected, GetSemanticConventionOptIn(configuration));
    }
}
