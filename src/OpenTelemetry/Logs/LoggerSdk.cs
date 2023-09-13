// <copyright file="LoggerSdk.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

/// <summary>
/// SDK <see cref="Logger"/> implementation.
/// </summary>
internal sealed class LoggerSdk : Logger
{
    private readonly LoggerProviderSdk loggerProvider;

    public LoggerSdk(
        LoggerProviderSdk loggerProvider,
        string? name)
        : base(name)
    {
        Guard.ThrowIfNull(loggerProvider);

        this.loggerProvider = loggerProvider;
    }

    /// <inheritdoc />
    public override void EmitLog(in LogRecordData data, in LogRecordAttributeList attributes)
    {
        var provider = this.loggerProvider;
        var processor = provider.Processor;
        if (processor != null)
        {
            var pool = provider.LogRecordPool;

            var logRecord = pool.Rent();

            logRecord.Data = data;
            logRecord.ILoggerData = default;

            logRecord.Logger = this;

            logRecord.Attributes = attributes.Export(ref logRecord.AttributeStorage);

            processor.OnEnd(logRecord);

            // Attempt to return the LogRecord to the pool. This will no-op
            // if a batch exporter has added a reference.
            pool.Return(logRecord);
        }
    }
}
