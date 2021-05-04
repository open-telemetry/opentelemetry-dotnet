using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace System.Diagnostics.Metrics
{
    public class Gauge<T> : MeterInstrument<T> where T : unmanaged
    {
        internal Gauge(Meter meter, string name, string? description, string? unit) :
            base(meter, name, description, unit)
        {
            Publish();
        }

        public void Set(T val)
        {
            RecordMeasurement(val);
        }

        public void Set(T val, params (string LabelName, object LabelValue)[] labels)
        {
            RecordMeasurement(val, labels);
        }
    }
}
