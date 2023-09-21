// <copyright file="TracePersistentStorageTransmissionHandler.cs" company="OpenTelemetry Authors">
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

using System.Timers;
using Google.Protobuf;
using OpenTelemetry.PersistentStorage.Abstractions;
using OpenTelemetry.PersistentStorage.FileSystem;
using OpenTelemetry.Proto.Collector.Trace.V1;

namespace OpenTelemetry.Exporter;

internal class TracePersistentStorageTransmissionHandler : OtlpExporterTransmissionHandler<ExportTraceServiceRequest>
{
    private PersistentBlobProvider persistentBlobProvider;
    private System.Timers.Timer timer;

    public TracePersistentStorageTransmissionHandler()
    {
        this.persistentBlobProvider = new FileBlobProvider(@"c:\temp\otlp\trace");
        this.timer = new System.Timers.Timer();
        this.timer.Elapsed += this.RetryRequestFromStorage;
        this.timer.Interval = 20000;
        this.timer.AutoReset = true;
        this.timer.Start();
    }

    protected override bool OnSubmitRequestExceptionThrown(ExportTraceServiceRequest request, Exception exception)
    {
        if (this.persistentBlobProvider.TryCreateBlob(request.ToByteArray(), out _))
        {
            return true;
        }

        return this.OnHandleDroppedRequest(request);
    }

    private void RetryRequestFromStorage(object sender, ElapsedEventArgs e)
    {
        int fileCount = 0;
        while (fileCount < 10)
        {
            if (this.persistentBlobProvider.TryGetBlob(out var blob))
            {
                if (blob != null && blob.TryLease(20000) && blob.TryRead(out var data))
                {
                    var request = new ExportTraceServiceRequest();
                    request.MergeFrom(data);
                    this.RetryRequest(request, out var exception);
                    if (exception == null)
                    {
                        blob.TryDelete();
                    }
                }
            }
            else
            {
                break;
            }

            fileCount++;
        }
    }
}
