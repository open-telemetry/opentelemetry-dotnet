using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace System.Diagnostics.Metrics
{
    public class Meter : IDisposable
    {
        List<MeterInstrument>? _instruments = new List<MeterInstrument>();

        public Meter(string name) : this(name, "") { }

        public Meter(string name, string version)
        {
            Name = name;
            Version = version;
            lock (MeterInstrumentCollection.Lock)
            {
                MeterInstrumentCollection.Instance.AddMeter(this);
            }
        }

        public Counter<T> CreateCounter<T>(string name, string? description = null, string? unit = null) where T:unmanaged
        {
            return new Counter<T>(this, name, description, unit);
        }

        public CounterFunc<T> CreateCounterFunc<T>(string name, Func<T> observeValue, string? description = null, string? unit = null) where T: unmanaged
        {
            return new CounterFunc<T>(this, name, observeValue, description, unit);
        }

        public CounterFunc<T> CreateCounterFunc<T>(string name, Func<IEnumerable<Measurement<T>>> observeValues, string? description = null, string? unit = null) where T: unmanaged
        {
            return new CounterFunc<T>(this, name, observeValues, description, unit);
        }

        public Gauge<T> CreateGauge<T>(string name, string? description = null, string? unit = null) where T: unmanaged
        {
            return new Gauge<T>(this, name, description, unit);
        }

        public Distribution<T> CreateDistribution<T>(string name, string? description = null, string? unit = null) where T: unmanaged
        {
            return new Distribution<T>(this, name, description, unit);
        }

        public string Name { get; }
        public string Version { get; }


        internal void PublishInstrument(MeterInstrument instrument)
        {
            lock (MeterInstrumentCollection.Lock)
            {
                if (_instruments != null) // if not disposed
                {
                    _instruments.Add(instrument);
                    MeterInstrumentCollection.Instance.PublishInstrument(instrument);
                }
            }
        }

        public void Dispose()
        {
            lock (MeterInstrumentCollection.Lock)
            {
                MeterInstrumentCollection.Instance.RemoveMeter(this);
                _instruments = null;
            }
        }

        internal IEnumerable<MeterInstrument> Instruments =>
            #if NET452
                (IEnumerable<MeterInstrument>?)_instruments ?? new MeterInstrument[0];
            #else
                (IEnumerable<MeterInstrument>?)_instruments ?? Array.Empty<MeterInstrument>();
            #endif
    }
}
