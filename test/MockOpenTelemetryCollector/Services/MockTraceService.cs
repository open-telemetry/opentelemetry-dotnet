// <copyright file="MockTraceService.cs" company="OpenTelemetry Authors">
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

using System.Threading.Tasks;
using Grpc.Core;
using OpenTelemetry.Proto.Collector.Trace.V1;

namespace MockOpenTelemetryCollector.Services;

internal class MockTraceService : TraceService.TraceServiceBase
{
    private readonly MockCollectorState state;

    public MockTraceService(MockCollectorState state)
    {
        this.state = state;
    }

    public override Task<ExportTraceServiceResponse> Export(ExportTraceServiceRequest request, ServerCallContext context)
    {
        var statusCode = this.state.NextStatus();
        if (statusCode != StatusCode.OK)
        {
            throw new RpcException(new Status(statusCode, "Error."));
        }

        return Task.FromResult(new ExportTraceServiceResponse());
    }
}
