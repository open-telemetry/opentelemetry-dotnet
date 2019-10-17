// <copyright file="DebuggingProcessor.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Export;

namespace Exporters
{
    public class DebuggingProcessor : SpanProcessor
    {
        public override void OnStart(Span span)
        {
            Debug.WriteLine($"[{DateTime.UtcNow:o}] OnStart {span.Name}");
        }

        public override void OnEnd(Span span)
        {
            Debug.WriteLine($"[{DateTime.UtcNow:o}] OnEnd {span.Name}");
        }

        public override Task ShutdownAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
