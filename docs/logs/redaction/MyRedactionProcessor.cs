// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using OpenTelemetry;
using OpenTelemetry.Logs;

internal sealed class MyRedactionProcessor : BaseProcessor<LogRecord>
{
    public override void OnEnd(LogRecord logRecord)
    {
        if (logRecord.Attributes != null)
        {
            logRecord.Attributes = new MyClassWithRedactionEnumerator(logRecord.Attributes);
        }
    }

    internal sealed class MyClassWithRedactionEnumerator : IReadOnlyList<KeyValuePair<string, object?>>
    {
        private readonly IReadOnlyList<KeyValuePair<string, object?>> state;

        public MyClassWithRedactionEnumerator(IReadOnlyList<KeyValuePair<string, object?>> state)
        {
            this.state = state;
        }

        public int Count => this.state.Count;

        public KeyValuePair<string, object?> this[int index]
        {
            get
            {
                var item = this.state[index];
                var entryVal = item.Value?.ToString();
                if (entryVal != null && entryVal.Contains("<secret>"))
                {
                    return new KeyValuePair<string, object?>(item.Key, "newRedactedValueHere");
                }

                return item;
            }
        }

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            for (var i = 0; i < this.Count; i++)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
