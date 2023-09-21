// <copyright file="OtlpExporterTransmissionHandler.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Exporter;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

namespace OpenTelemetry.ExporterOpenTelemetryProtocol.Implementation.Retry;

internal class OtlpExporterTransmissionHandler<T>
{
    internal IExportClient<T> ExportClient;

    public OtlpExporterOptions Options { get; internal set; }

    public virtual bool SubmitRequest(T request)
    {
        try
        {
            return this.ExportClient.SendExportRequest(request);
        }
        catch (Exception ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.ExportMethodException(ex);
            return this.OnSubmitRequestExceptionThrown(request, ex);
        }
    }

    protected virtual bool RetryRequest(T request, out Exception exception)
    {
        try
        {
            var result = this.ExportClient.SendExportRequest(request);
            exception = null;
            return result;
        }
        catch (Exception ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.ExportMethodException(ex, isRetry: true);
            exception = ex;
            return false;
        }
    }

    protected virtual bool OnSubmitRequestExceptionThrown(T request, Exception exception)
    {
        return this.OnHandleDroppedRequest(request);
    }

    protected virtual bool OnHandleDroppedRequest(T request)
    {
        return false;
    }
}
