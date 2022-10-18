// <copyright file="MyRedactionProcessor.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Redaction;

internal class MyRedactionProcessor : BaseProcessor<LogRecord>
{
    public override void OnEnd(LogRecord logRecord)
    {
        if (logRecord.State == null)
        {
            // When State is null, OTel SDK guarantees StateValues is populated
            // TODO: Debug.Assert?
            logRecord.StateValues = new MyClassWithRedactionEnumerator(logRecord.StateValues);
        }
        else if (logRecord.State is IReadOnlyList<KeyValuePair<string, object>> listOfKvp)
        {
            logRecord.State = new MyClassWithRedactionEnumerator(listOfKvp);
        }
    }
}
