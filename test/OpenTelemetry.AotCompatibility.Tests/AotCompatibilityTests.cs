// <copyright file="AotCompatibilityTests.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.AotCompatibility.Tests;

public class AotCompatibilityTests
{
    private readonly ITestOutputHelper testOutputHelper;

    public AotCompatibilityTests(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;
    }

    /// <summary>
    /// This test ensures that the intended APIs of the OpenTelemetry.AotCompatibility.TestApp libraries are
    /// trimming and NativeAOT compatible.
    ///
    /// This test follows the instructions in https://learn.microsoft.com/dotnet/core/deploying/trimming/prepare-libraries-for-trimming#show-all-warnings-with-sample-application
    ///
    /// If this test fails, it is due to adding trimming and/or AOT incompatible changes
    /// to code that is supposed to be compatible.
    ///
    /// To diagnose the problem, inspect the test output which will contain the trimming and AOT errors. For example:
    ///
    /// error IL2091: 'T' generic argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicConstructors'.
    /// </summary>
    [Fact]
    public void EnsureAotCompatibility()
    {
        string[] paths = { @"..", "..", "..", "..", "OpenTelemetry.AotCompatibility.TestApp" };
        string testAppPath = Path.Combine(paths);
        string testAppProject = "OpenTelemetry.AotCompatibility.TestApp.csproj";

        // ensure we run a clean publish every time
        DirectoryInfo testObjDir = new DirectoryInfo(Path.Combine(testAppPath, "obj"));
        if (testObjDir.Exists)
        {
            testObjDir.Delete(recursive: true);
        }

        var process = new Process
        {
            // set '-nodereuse:false /p:UseSharedCompilation=false' so the MSBuild and Roslyn server processes don't hang around, which may hang the test in CI
            StartInfo = new ProcessStartInfo("dotnet", $"publish {testAppProject} --self-contained -nodereuse:false /p:UseSharedCompilation=false")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = testAppPath,
            },
        };

        var expectedOutput = new System.Text.StringBuilder();
        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                this.testOutputHelper.WriteLine(e.Data);
                expectedOutput.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();

        Assert.True(process.WaitForExit(milliseconds: 240_000), "dotnet publish command timed out after 240 seconds.");
        Assert.True(process.ExitCode == 0, "Publishing the AotCompatibility app failed. See test output for more details.");

        var warnings = expectedOutput.ToString().Split('\n', '\r').Where(line => line.Contains("warning IL"));
        Assert.Equal(30, warnings.Count());
    }
}
