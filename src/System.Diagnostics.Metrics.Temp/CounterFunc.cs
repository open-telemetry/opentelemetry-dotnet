using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace System.Diagnostics.Metrics
{
    public class CounterFunc<T> : ObservableMeterInstrument<T> where T: unmanaged
    {
        // This is either a Func<T> or an Func<IEnumerable<Measurement<T>>>
        object _observeValueFunc;

        public CounterFunc(Meter meter, string name, Func<T> observeValue, string? description, string? unit) :
            base(meter, name, description, unit)
        {
            _observeValueFunc = observeValue;
            Publish();
        }

        public CounterFunc(Meter meter, string name, Func<IEnumerable<Measurement<T>>> observeValues, string? description, string? unit) :
            base(meter, name, description, unit)
        {
            _observeValueFunc = observeValues;
            Publish();
        }

        protected override IEnumerable<Measurement<T>> Observe()
        {
            if (_observeValueFunc is Func<T>)
            {
                T value = ((Func<T>)_observeValueFunc)();
                return new Measurement<T>[] { new Measurement<T>(value) };
            }
            else
            {
                return ((Func<IEnumerable<Measurement<T>>>)_observeValueFunc)();
            }
        }
    }
}
