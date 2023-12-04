// <copyright file="CustomTextMapPropagator.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Tests;

internal sealed class CustomTextMapPropagator : TextMapPropagator
{
#pragma warning disable SA1010
    public List<string> ExtractValues = [];
    public Dictionary<string, Func<PropagationContext, string>> InjectValues = [];
    private static readonly PropagationContext DefaultPropagationContext = default;

    public Action<PropagationContext> Injected { get; set; }

    public override ISet<string> Fields => null;

    public override PropagationContext Extract<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>> getter)
    {
        if (this.ExtractValues.Count == 2)
        {
            return new PropagationContext(
                new ActivityContext(
                    ActivityTraceId.CreateFromString(this.ExtractValues[0].ToCharArray()),
                    ActivitySpanId.CreateFromString(this.ExtractValues[1].ToCharArray()),
                    ActivityTraceFlags.Recorded),
                default);
        }

        return DefaultPropagationContext;
    }

    public override void Inject<T>(PropagationContext context, T carrier, Action<T, string, string> setter)
    {
        foreach (var kv in this.InjectValues)
        {
            setter(carrier, kv.Key, kv.Value.Invoke(context));
        }

        this.Injected?.Invoke(context);
    }
}
