// <copyright file="ConsoleTagTransformer.cs" company="OpenTelemetry Authors">
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

using System;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter;

internal sealed class ConsoleTagTransformer : TagTransformer<string>
{
    private ConsoleTagTransformer()
    {
    }

    public static ConsoleTagTransformer Instance { get; } = new();

    protected override string TransformIntegralTag(string key, long value) => $"{key}: {value}";

    protected override string TransformFloatingPointTag(string key, double value) => $"{key}: {value}";

    protected override string TransformBooleanTag(string key, bool value) => $"{key}: {(value ? "true" : "false")}";

    protected override string TransformStringTag(string key, string value) => $"{key}: {value}";

    protected override string TransformArrayTag(string key, Array array)
        => this.TransformStringTag(key, System.Text.Json.JsonSerializer.Serialize(array));
}
