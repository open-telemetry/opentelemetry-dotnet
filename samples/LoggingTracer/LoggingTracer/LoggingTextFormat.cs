// <copyright file="LoggingTextFormat.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;

namespace LoggingTracer
{
    public sealed class LoggingTextFormat : ITextFormat
    {
        /// <inheritdoc/>
        public ISet<string> Fields => null;

        /// <inheritdoc/>
        public SpanContext Extract<T>(T carrier, Func<T, string, IEnumerable<string>> getter)
        {
            Logger.Log("LoggingTextFormat.Extract(...)");
            return SpanContext.Blank;
        }

        /// <inheritdoc/>
        public void Inject<T>(SpanContext spanContext, T carrier, Action<T, string, string> setter)
        {
            Logger.Log($"LoggingTextFormat.Inject({spanContext}, ...)");
        }
    }
}
