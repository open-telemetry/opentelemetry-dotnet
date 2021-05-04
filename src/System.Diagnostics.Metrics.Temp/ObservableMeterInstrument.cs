using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace System.Diagnostics.Metrics
{
    public abstract class ObservableMeterInstrument<T> : MeterInstrument where T : unmanaged
    {
        protected ObservableMeterInstrument(Meter meter, string name, string? description, string? unit) : base(meter, name, description, unit) { }

        protected abstract IEnumerable<Measurement<T>> Observe();
        public override bool IsObservable => true;

        internal override void Observe(MeterInstrumentListener listener, object? cookie)
        {
            foreach (Measurement<T> m in Observe())
            {
                listener.OnMeasurement(this, m.Value, m.Labels.ToArray(), cookie);
            }
        }
    }

    public struct Measurement<T> where T : unmanaged
    {
        public Measurement(T value, IEnumerable<(string, object)> labels)
        {
            Labels = labels;
            Value = value;
        }

        public Measurement(T value, params (string, object)[] labels)
        {
            Labels = labels;
            Value = value;
        }

        public IEnumerable<(string, object)> Labels { get; }
        public T Value { get; }
    }
}
