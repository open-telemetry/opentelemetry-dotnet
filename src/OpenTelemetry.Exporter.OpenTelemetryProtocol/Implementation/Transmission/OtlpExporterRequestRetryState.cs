// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;

internal sealed class OtlpExporterRequestRetryState<TRequest>
{
    public OtlpExporterRequestRetryState(TRequest request, int submissionCount)
    {
        this.Request = request;
        this.SubmissionCount = submissionCount;
    }

    public TRequest Request { get; }

    public int SubmissionCount { get; set; }
}
