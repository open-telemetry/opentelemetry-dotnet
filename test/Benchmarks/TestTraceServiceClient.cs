// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

extern alias OpenTelemetryProtocol;

using Grpc.Core;
using OpenTelemetryProtocol::OpenTelemetry.Proto.Collector.Trace.V1;

namespace Benchmarks;

internal class TestTraceServiceClient : TraceService.TraceServiceClient
{
    public override ExportTraceServiceResponse Export(ExportTraceServiceRequest request, Metadata headers = null, DateTime? deadline = null, CancellationToken cancellationToken = default)
    {
        return new ExportTraceServiceResponse();
    }
}