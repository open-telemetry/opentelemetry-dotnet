﻿// <copyright file="NoopSpanExporter.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Export
{
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class NoopSpanExporter : ISpanExporter
    {
        public void AddSpan(Span span)
        {
        }

        public Task ExportAsync(Span export, CancellationToken token)
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }

        public void RegisterHandler(string name, IHandler handler)
        {
        }

        public void UnregisterHandler(string name)
        {
        }
    }
}
