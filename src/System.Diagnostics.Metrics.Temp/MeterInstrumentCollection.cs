using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace System.Diagnostics.Metrics
{
    internal class MeterInstrumentCollection
    {
        public static MeterInstrumentCollection Instance = new MeterInstrumentCollection();

        // Even if we had multiple exposed instances of this collection in the future
        // this lock also synchronizes access to per-metric subscription lists so it
        // needs to remain global (or metric subscription lists need to be changed)
        static internal object Lock = new object();

        List<Meter> _meters = new List<Meter>();
        List<MeterInstrumentListener> _instrumentPublishedListeners = new List<MeterInstrumentListener>();
        List<MeterInstrumentListener> _instrumentDisposedListeners = new List<MeterInstrumentListener>();

        public void AddMeter(Meter meter)
        {
            lock (Lock)
            {
                _meters.Add(meter);
                foreach (MeterInstrumentListener listener in _instrumentPublishedListeners)
                {
                    foreach (MeterInstrument instrument in meter.Instruments)
                    {
                        NotifyListenerInstrumentPublished(listener, instrument);
                    }
                }
            }
        }

        public void RemoveMeter(Meter meter)
        {
            lock (Lock)
            {
                _meters.Remove(meter);
                foreach (MeterInstrumentListener listener in _instrumentDisposedListeners.ToArray())
                {
                    foreach (MeterInstrument instrument in meter.Instruments)
                    {
                        NotifyInstrumentDisposed(listener, instrument);
                    }
                }
            }
        }

        public void PublishInstrument(MeterInstrument instrument)
        {
            Debug.Assert(Monitor.IsEntered(Lock));
            foreach (MeterInstrumentListener listener in _instrumentPublishedListeners)
            {
                NotifyListenerInstrumentPublished(listener, instrument);
            }
        }

        public void AddInstrumentPublishedListener(MeterInstrumentListener listener)
        {
            lock (Lock)
            {
                _instrumentPublishedListeners.Add(listener);
                VisitInstruments(i => NotifyListenerInstrumentPublished(listener, i));
            }
        }

        public void RemoveInstrumentedPublishedListener(MeterInstrumentListener listener)
        {
            lock (Lock)
            {
                _instrumentPublishedListeners.Remove(listener);
            }
        }

        public void AddInstrumentDisposedListener(MeterInstrumentListener listener)
        {
            lock (Lock)
            {
                _instrumentDisposedListeners.Add(listener);
            }
        }

        public void RemoveInstrumentDisposedListener(MeterInstrumentListener listener)
        {
            lock (Lock)
            {
                _instrumentDisposedListeners.Remove(listener);
            }
        }

        public void VisitInstruments(Action<MeterInstrument> visitor)
        {
            lock(Lock)
            {
                foreach (Meter meter in _meters)
                {
                    foreach (MeterInstrument instrument in meter.Instruments)
                    {
                        visitor(instrument);
                    }
                }
            }
        }

        void NotifyListenerInstrumentPublished(MeterInstrumentListener listener, MeterInstrument instrument)
        {
            listener.OnMeterInstrumentPublished(instrument);
        }

        void NotifyInstrumentDisposed(MeterInstrumentListener listener, MeterInstrument instrument)
        {
            listener.OnMeterInstrumentDisposed(instrument);
        }
    }
}
