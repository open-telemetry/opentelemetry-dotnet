// <copyright file="SdkTests.cs" company="OpenTelemetry Authors">
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

#nullable enable

using Xunit;

namespace OpenTelemetry.Tests;

public class SdkTests
{
    [Theory]
    [InlineData(null, "1.0.0")]
    [InlineData("1.5.0", "1.5.0")]
    [InlineData("1.0.0.0", "1.0.0.0")]
    [InlineData("1.0-beta.1", "1.0-beta.1")]
    [InlineData("1.5.0-alpha.1.40+807f703e1b4d9874a92bd86d9f2d4ebe5b5d52e4", "1.5.0-alpha.1.40")]
    [InlineData("1.5.0-rc.1+807f703e1b4d9874a92bd86d9f2d4ebe5b5d52e4", "1.5.0-rc.1")]
    [InlineData("8.0", "8.0")]
    [InlineData("8", "8")]
    [InlineData("8.0.1.18-alpha1", "8.0.1.18-alpha1")]
    public void ParseAssemblyInformationalVersionTests(string? informationalVersion, string expectedVersion)
    {
        var actualVersion = Sdk.ParseAssemblyInformationalVersion(informationalVersion);

        Assert.Equal(expectedVersion, actualVersion);
    }
}
