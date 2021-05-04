using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace System.Diagnostics.Metrics
{
    public abstract class MeterInstrument
    {
        protected MeterInstrument(Meter meter, string name, string? description, string? unit)
        {
            Meter = meter;
            Name = name;
            Description = description;
            Unit = unit;
        }

        internal struct ListenerSubscription
        {
            public MeterInstrumentListener Listener;
            public object? Cookie;
        }


        [StructLayout(LayoutKind.Sequential)]
        internal struct ThreeLabels
        {
            public (string LabelName, object LabelValue) Label1;
            public (string LabelName, object LabelValue) Label2;
            public (string LabelName, object LabelValue) Label3;
        }

#if NET452
        internal ListenerSubscription[] _subscriptions = new ListenerSubscription[0];
#else
        internal ListenerSubscription[] _subscriptions = Array.Empty<ListenerSubscription>();
#endif

        public Meter Meter { get; }
        public string Name { get; }
        public string? Description { get; }
        public string? Unit { get; }
        public bool Enabled => _subscriptions.Length > 0 || IsObservable;

        /// <summary>
        /// Adds the instrument to the list maintained on Meter which in turn
        /// makes it visible to listeners.
        /// </summary>
        protected void Publish()
        {
            Meter.PublishInstrument(this);
        }

        public virtual bool IsObservable => false;

        internal virtual void Observe(MeterInstrumentListener listener, object? cookie)
        {
            // Observe should only be called on Observable metrics
            // The call needs to be here because ObservableMeterInstrument<T> would require
            // the caller to know the generic T and they don't know it.
            // Alternative impls:
            // 1. Create a non-generic ObservableMeterInstrument
            // that is a base class of ObservableMeterInstrument<T> and put this method there
            // 2. Make a hard-coded list of every supported T and then directly invoke
            // ObservableMeterInstrument<T>.Observe(listener, cookie)
            Debug.Assert(false);
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Adds a new subscription or updates the old subscription to use the new cookie.
        /// Returns true when adding a new subscription, returns false if a subscription already exists
        /// regardless of previous cookie
        /// </summary>
        internal bool AddOrUpdateSubscription(MeterInstrumentListener listener, object? listenerCookie, out object? previousCookie)
        {
            // only push metrics should have subscriptions
            Debug.Assert(!IsObservable);
            // this should only be called under the metric collection lock
            Debug.Assert(Monitor.IsEntered(MeterInstrumentCollection.Lock));

            ListenerSubscription[] subs;
            for (int i = 0; i < _subscriptions.Length; i++)
            {
                if (_subscriptions[i].Listener == listener)
                {
                    previousCookie = _subscriptions[i].Cookie;
                    if (_subscriptions[i].Cookie != listenerCookie)
                    {
                        // because the array is read lock-free we always allocate a new
                        // one when making any change. If we wanted to get really detailed
                        // about what consistency guarantees we were making maybe we could
                        // avoid the alloc but subscription isn't a perf critical path so
                        // lets keep it simple.
                        subs = new ListenerSubscription[_subscriptions.Length];
                        Array.Copy(_subscriptions, subs, _subscriptions.Length);
                        subs[i].Cookie = listenerCookie;
                        _subscriptions = subs;
                    }
                    return false;
                }
            }

            subs = new ListenerSubscription[_subscriptions.Length + 1];
            Array.Copy(_subscriptions, subs, _subscriptions.Length);
            subs[_subscriptions.Length].Listener = listener;
            subs[_subscriptions.Length].Cookie = listenerCookie;
            _subscriptions = subs;
            previousCookie = null;
            return true;
        }

        /// <summary>
        /// Returns true if the listener was previously subscribed
        /// </summary>
        /// <param name="listener"></param>
        /// <param name="cookie"></param>
        /// <returns></returns>
        internal bool RemoveSubscription(MeterInstrumentListener listener, out object? cookie)
        {
            // only push metrics should have subscriptions
            Debug.Assert(!IsObservable);

            // this should only be called under metric collection lock
            Debug.Assert(Monitor.IsEntered(MeterInstrumentCollection.Lock));

            for (int i = 0; i < _subscriptions.Length; i++)
            {
                if (_subscriptions[i].Listener == listener)
                {
                    cookie = _subscriptions[i].Cookie;
                    ListenerSubscription[] subs = new ListenerSubscription[_subscriptions.Length - 1];
                    Array.Copy(_subscriptions, subs, i);
                    Array.Copy(_subscriptions, i + 1, subs, i, _subscriptions.Length - i - 1);
                    _subscriptions = subs;
                    return true;
                }
            }
            cookie = null;
            return false;
        }
    }

    public abstract class MeterInstrument<T> : MeterInstrument where T : unmanaged
    {
        protected MeterInstrument(Meter meter, string name, string? description, string? unit) : base(meter, name, description, unit) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void RecordMeasurement(T val) =>
#if NET452
                RecordMeasurement(val, new (string LabelName, object LabelValue)[0]);
#else
                RecordMeasurement(val, Array.Empty<(string LabelName, object LabelValue)>());
#endif
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void RecordMeasurement(T val, (string LabelName, object LabelValue) label1)
        {
#if NET5_0
                ReadOnlySpan<(string LabelName, object LabelValue)> labels = MemoryMarshal.CreateReadOnlySpan(ref label1, 1);
#else
                var oneLabels = new (string LabelName, object labelValue)[1];
                oneLabels[0] = label1;
                var labels = new ReadOnlySpan<(string LabelName, object LabelValue)>(oneLabels);
#endif
            RecordMeasurement(val, labels);
        }

#if NET5_0
        [SkipLocalsInit]
        #endif
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void RecordMeasurement(T val,
            (string LabelName, object LabelValue) label1,
            (string LabelName, object LabelValue) label2)
        {
#if NET5_0
            ThreeLabels threeLabels = new ThreeLabels();
            threeLabels.Label1 = label1;
            threeLabels.Label2 = label2;
            ReadOnlySpan<(string LabelName, object LabelValue)> labels = MemoryMarshal.CreateReadOnlySpan(ref threeLabels.Label1, 2);
#else
            var twoLabels = new (string LabelName, object labelValue)[2];
            twoLabels[0] = label1;
            twoLabels[1] = label2;
            var labels = new ReadOnlySpan<(string LabelName, object LabelValue)>(twoLabels);
#endif
            RecordMeasurement(val, labels);
        }

#if NET5_0
        [SkipLocalsInit]
#endif
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void RecordMeasurement(T val,
            (string LabelName, object LabelValue) label1,
            (string LabelName, object LabelValue) label2,
            (string LabelName, object LabelValue) label3)
        {
#if NET5_0
                ThreeLabels threeLabels = new ThreeLabels();
                threeLabels.Label1 = label1;
                threeLabels.Label2 = label2;
                threeLabels.Label3 = label3;
                ReadOnlySpan<(string LabelName, object LabelValue)> labels = MemoryMarshal.CreateReadOnlySpan(ref threeLabels.Label1, 3);
#else
                var threeLabels = new (string LabelName, object labelValue)[3];
                threeLabels[0] = label1;
                threeLabels[1] = label2;
                threeLabels[2] = label3;
                var labels = new ReadOnlySpan<(string LabelName, object LabelValue)>(threeLabels);
#endif
            RecordMeasurement(val, labels);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void RecordMeasurement(T val, ReadOnlySpan<(string LabelName, object LabelValue)> labels)
        {
            // this captures a snapshot, _subscriptions array could be replaced while
            // we are invoking callbacks
            ListenerSubscription[] subscriptions = _subscriptions;
            for (int i = 0; i < subscriptions.Length; i++)
            {
                subscriptions[i].Listener.OnMeasurement(this, val, labels, subscriptions[i].Cookie);
            }
        }

    }
}
