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

    /// <summary>
    /// Sends export request to the server.
    /// </summary>
    /// <param name="request">The request to send to the server.</param>
    /// <returns>True if the request is sent successfully or else false.</returns>
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

    /// <summary>
    /// Retries sending request to the server.
    /// </summary>
    /// <param name="request">The request to send to the server.</param>
    /// <param name="exception">Exception encountered when trying to send request.</param>
    /// <returns>True if the request is sent successfully or else false.</returns>
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

    /// <summary>
    /// Callback to call when encountered exception while sending request to server.
    /// </summary>
    /// <param name="request">The request that was attempted to send to the server.</param>
    /// <param name="exception">Exception that was encountered during request processing.</param>
    /// <returns>True or False, based on the implementation of handling errors.</returns>
    protected virtual bool OnSubmitRequestExceptionThrown(T request, Exception exception)
    {
        return this.OnHandleDroppedRequest(request);
    }

    /// <summary>
    /// Action to take when dropping request.
    /// </summary>
    /// <param name="request">The request that was attempted to send to the server.</param>
    /// <returns>True or False, based on the implementation.</returns>
    protected virtual bool OnHandleDroppedRequest(T request)
    {
        return false;
    }
}
