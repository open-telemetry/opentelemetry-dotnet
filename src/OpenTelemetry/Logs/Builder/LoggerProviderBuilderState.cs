// <copyright file="LoggerProviderBuilderState.cs" company="OpenTelemetry Authors">
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

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace OpenTelemetry.Logs;

/// <summary>
/// Stores state used to build a <see cref="LoggerProvider"/>.
/// </summary>
internal sealed class LoggerProviderBuilderState : ProviderBuilderState<LoggerProviderBuilderSdk, LoggerProviderSdk>
{
    private LoggerProviderBuilderSdk? builder;

    public LoggerProviderBuilderState(IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
    }

    public override LoggerProviderBuilderSdk Builder
        => this.builder ??= new LoggerProviderBuilderSdk(this);

    public List<BaseProcessor<LogRecord>> Processors { get; } = new();

    public void AddProcessor(BaseProcessor<LogRecord> processor)
    {
        Debug.Assert(processor != null, "processor was null");

        this.Processors.Add(processor!);
    }
}
