// <copyright file="OTelEnvSamplerDetectorTests.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using Xunit;

namespace OpenTelemetry.Trace.Tests;

public class OTelEnvSamplerDetectorTests : IDisposable
{
    public OTelEnvSamplerDetectorTests()
    {
        Environment.SetEnvironmentVariable(OTelEnvSamplerDetector.OTelTracesSamplerEnvVarKey, null);
        Environment.SetEnvironmentVariable(OTelEnvSamplerDetector.OTelTracesSamplerArgEnvVarKey, null);
    }

    public static IEnumerable<object[]> SupportedSamplers =>
        new List<object[]>
        {
            new object[] { "always_on", null, "AlwaysOnSampler" },
            new object[] { "always_off", null, "AlwaysOffSampler" },
            new object[] { "traceidratio", null, "TraceIdRatioBasedSampler{1.000000}" },
            new object[] { "traceidratio", "2", "TraceIdRatioBasedSampler{1.000000}" },
            new object[] { "traceidratio", "-1", "TraceIdRatioBasedSampler{1.000000}" },
            new object[] { "traceidratio", "non-a-number", "TraceIdRatioBasedSampler{1.000000}" },
            new object[] { "traceidratio", "0.25", "TraceIdRatioBasedSampler{0.250000}" },
            new object[] { "parentbased_always_on", null, "ParentBased{AlwaysOnSampler}" },
            new object[] { "parentbased_always_off", null, "ParentBased{AlwaysOffSampler}" },
            new object[] { "parentbased_traceidratio", null, "ParentBased{TraceIdRatioBasedSampler{1.000000}}" },
            new object[] { "parentbased_traceidratio", "0.25", "ParentBased{TraceIdRatioBasedSampler{0.250000}}" },
        };

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(OTelEnvSamplerDetector.OTelTracesSamplerEnvVarKey, null);
        Environment.SetEnvironmentVariable(OTelEnvSamplerDetector.OTelTracesSamplerArgEnvVarKey, null);

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void OTelEnvSamplerDetector_OTelTracesSamplerEnvVarKey()
    {
        Assert.Equal("OTEL_TRACES_SAMPLER", OTelEnvSamplerDetector.OTelTracesSamplerEnvVarKey);
    }

    [Fact]
    public void OTelEnvSamplerDetector_OTelTracesSamplerArgEnvVarKey()
    {
        Assert.Equal("OTEL_TRACES_SAMPLER_ARG", OTelEnvSamplerDetector.OTelTracesSamplerArgEnvVarKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("non-supported-value")]
    public void OTelEnvSamplerDetector_NonSupportedValues(string oTelTracesSamplerEnvVarValue)
    {
        Environment.SetEnvironmentVariable(OTelEnvSamplerDetector.OTelTracesSamplerEnvVarKey, oTelTracesSamplerEnvVarValue);

        var sampler = new OTelEnvSamplerDetector().Detect();

        Assert.Null(sampler);
    }

    [Theory]
    [MemberData(nameof(SupportedSamplers))]
    public void OTelEnvSamplerDetector_NullEnvVar(string oTelTracesSamplerEnvVarValue, string oTelTracesSamplerArgEnvVarValue, string expectedDescription)
    {
        Environment.SetEnvironmentVariable(OTelEnvSamplerDetector.OTelTracesSamplerEnvVarKey, oTelTracesSamplerEnvVarValue);
        Environment.SetEnvironmentVariable(OTelEnvSamplerDetector.OTelTracesSamplerArgEnvVarKey, oTelTracesSamplerArgEnvVarValue);

        var sampler = new OTelEnvSamplerDetector().Detect();

        Assert.Equal(expectedDescription, sampler.Description);
    }
}
