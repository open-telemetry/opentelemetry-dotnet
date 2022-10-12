// <copyright file="MyCustomDistributedContextPropagator.cs" company="OpenTelemetry Authors">
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

namespace Examples.AspNetCore
{
    public class MyCustomDistributedContextPropagator : DistributedContextPropagator
    {
        public override IReadOnlyCollection<string> Fields => throw new NotImplementedException();

        public override IEnumerable<KeyValuePair<string, string?>>? ExtractBaggage(object? carrier, PropagatorGetterCallback? getter)
        {
            throw new NotImplementedException();
        }

        public override void ExtractTraceIdAndState(object? carrier, PropagatorGetterCallback? getter, out string? traceId, out string? traceState)
        {
            throw new NotImplementedException();
        }

        public override void Inject(Activity? activity, object? carrier, PropagatorSetterCallback? setter)
        {
            throw new NotImplementedException();
        }
    }
}
