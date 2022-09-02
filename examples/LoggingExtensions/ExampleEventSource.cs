// <copyright file="ExampleEventSource.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics.Tracing;

namespace Examples.LoggingExtensions;

[EventSource(Name = EventSourceName)]
internal sealed class ExampleEventSource : EventSource
{
    public const string EventSourceName = "OpenTelemetry-ExampleEventSource";

    public static ExampleEventSource Log { get; } = new();

    [Event(1, Message = "Example event written with '{0}' reason", Level = EventLevel.Informational)]
    public void ExampleEvent(string reason)
    {
        this.WriteEvent(1, reason);
    }
}
