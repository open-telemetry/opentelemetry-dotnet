// <copyright file="SamplerBuilderTests.cs" company="OpenTelemetry Authors">
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
using Moq;
using Xunit;

namespace OpenTelemetry.Trace.Tests;

public class SamplerBuilderTests : IDisposable
{
    public SamplerBuilderTests()
    {
        Environment.SetEnvironmentVariable(OTelEnvSamplerDetector.OTelTracesSamplerEnvVarKey, null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(OTelEnvSamplerDetector.OTelTracesSamplerEnvVarKey, null);

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Build_ReturnsByDefaultParentBasedAlwaysOn()
    {
        var sampler = SamplerBuilder.CreateEmpty().Build();

        Assert.Equal("ParentBased{AlwaysOnSampler}", sampler.Description);
    }

    [Fact]
    public void Build_SupportsSetDefaultSampler()
    {
        var sampler = SamplerBuilder.CreateEmpty()
            .SetDefaultSampler(new AlwaysOffSampler())
            .Build();

        Assert.Equal("AlwaysOffSampler", sampler.Description);
    }

    [Fact]
    public void Build_ReturnsDefaultValueWhenDetectorsReturnNull()
    {
        Mock<ISamplerDetector> detector1 = new Mock<ISamplerDetector>();
        detector1.Setup(x => x.Detect()).Returns(default(Sampler));
        Mock<ISamplerDetector> detector2 = new Mock<ISamplerDetector>();
        detector1.Setup(x => x.Detect()).Returns(default(Sampler));

        var sampler = SamplerBuilder.CreateEmpty()
            .RegisterDetector(detector1.Object)
            .RegisterDetector(detector2.Object)
            .Build();

        Assert.Equal("ParentBased{AlwaysOnSampler}", sampler.Description);
    }

    [Fact]
    public void Build_ReturnsFirstNonNullSamplerFromDetectors()
    {
        Mock<ISamplerDetector> detector1 = new Mock<ISamplerDetector>();
        detector1.Setup(x => x.Detect()).Returns(default(Sampler));
        Mock<ISamplerDetector> detector2 = new Mock<ISamplerDetector>();
        detector1.Setup(x => x.Detect()).Returns(new AlwaysOffSampler());
        Mock<ISamplerDetector> detector3 = new Mock<ISamplerDetector>();
        detector2.Setup(x => x.Detect()).Returns(new AlwaysOnSampler());

        var sampler = SamplerBuilder.CreateEmpty()
            .RegisterDetector(detector1.Object)
            .RegisterDetector(detector2.Object)
            .RegisterDetector(detector3.Object)
            .SetDefaultSampler(new AlwaysOffSampler())
            .Build();

        Assert.Equal("AlwaysOffSampler", sampler.Description);
    }

    [Fact]
    public void Clear_RemovesEffectsOfSetDefaultSamplerAndEnvironmentVariableDetector()
    {
        Environment.SetEnvironmentVariable(OTelEnvSamplerDetector.OTelTracesSamplerEnvVarKey, "always_on");

        var sampler = SamplerBuilder.CreateEmpty()
            .RegisterEnvironmentVariableDetector()
            .SetDefaultSampler(new AlwaysOffSampler())
            .Clear()
            .Build();

        Assert.Equal("ParentBased{AlwaysOnSampler}", sampler.Description);
    }

    [Fact]
    public void CrateDefaults_RegistersEnvironmentalVariableDetector()
    {
        Environment.SetEnvironmentVariable(OTelEnvSamplerDetector.OTelTracesSamplerEnvVarKey, "always_on");

        var sampler = SamplerBuilder.CreateDefault().Build();

        Assert.Equal("AlwaysOnSampler", sampler.Description);
    }
}
