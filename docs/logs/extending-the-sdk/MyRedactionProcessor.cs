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

using System;
using System.Collections.Generic;
using System.Text;
using OpenTelemetry;
using OpenTelemetry.Logs;

internal class MyRedactionProcessor : BaseProcessor<LogRecord>
{
    private readonly string name;

    public MyRedactionProcessor(string name)
    {
        this.name = name;
    }

    public override void OnEnd(LogRecord logRecord)
    {
        var listKvp = logRecord.State as IReadOnlyList<KeyValuePair<string, object>>;
        int kvpCount = listKvp == null ? 0 : listKvp.Count;

        Console.WriteLine($"{this.name}.OnEnd(LogRecord.State before redaction: {logRecord.State})");
        var newStateAsList = new List<KeyValuePair<string, object>>();
        for (int i = 0; i < kvpCount; ++i)
        {
            var entry = listKvp[i];
            if (kvpCount > 1 && StringComparer.Ordinal.Equals(entry.Key, "{OriginalFormat}"))
            {
                continue;
            }

            if (entry.Value is string str && str.Contains("sensitive information"))
            {
                newStateAsList.Add(new KeyValuePair<string, object>("redactedKey", "redactedVal"));
            }
        }

        logRecord.State = newStateAsList;
        var newLogRecordStateAsList = logRecord.State as IReadOnlyList<KeyValuePair<string, object>>;

        StringBuilder sb = new StringBuilder();
        foreach (var entry in newLogRecordStateAsList)
        {
            sb.Append(entry.Key + ", ");
            sb.Append(entry.Value + ".");
        }

        Console.WriteLine($"{this.name}.OnEnd(LogRecord.State after redaction: {sb})");
    }
}
