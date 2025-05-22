// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Trace;

namespace OpenTelemetry.Tests;

internal sealed class RecordOnlySampler : TestSampler
{
    public override SamplingResult ShouldSample(in SamplingParameters param)
    {
        return new SamplingResult(SamplingDecision.RecordOnly);
    }
}
