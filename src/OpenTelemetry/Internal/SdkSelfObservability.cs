// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry.Metrics;

namespace OpenTelemetry;

/// <summary>
/// Shared infrastructure for SDK self-observability metrics.
/// </summary>
internal static class SdkSelfObservability
{
    /// <summary>
    /// The name of the <see cref="System.Diagnostics.Metrics.Meter"/> used for SDK self-observability metrics.
    /// </summary>
    /// <remarks>
    /// This is a constant so that it can be referenced without triggering the static
    /// initialization of this class, which would create the instruments.
    /// </remarks>
    internal const string MeterName = "otel.sdk.experimental";

    internal static readonly Meter Meter = MeterFactory.Create(
        typeof(SdkSelfObservability), semanticConventionsVersion: null, name: MeterName);

    internal static readonly Counter<long> LogProcessedCounter = Meter.CreateCounter<long>(
        "otel.sdk.processor.log.processed",
        "{log_record}",
        "The number of log records for which the processing has finished, either successful or failed.");

    internal static readonly Counter<long> SpanProcessedCounter = Meter.CreateCounter<long>(
        "otel.sdk.processor.span.processed",
        "{span}",
        "The number of spans for which the processing has finished, either successful or failed.");

    internal static readonly object ProcessorQueueRegistrationsLock = new();
    internal static readonly List<ProcessorQueueRegistration> LogProcessorQueueRegistrations = [];
    internal static readonly List<ProcessorQueueRegistration> SpanProcessorQueueRegistrations = [];

    internal static readonly ObservableUpDownCounter<long> LogQueueSize = Meter.CreateObservableUpDownCounter(
        "otel.sdk.processor.log.queue.size",
        () => ObserveProcessorQueues(LogProcessorQueueRegistrations, observeCapacity: false),
        "{log_record}",
        "The number of log records in the queue of a given instance of an SDK log processor.");

    internal static readonly ObservableUpDownCounter<long> LogQueueCapacity = Meter.CreateObservableUpDownCounter(
        "otel.sdk.processor.log.queue.capacity",
        () => ObserveProcessorQueues(LogProcessorQueueRegistrations, observeCapacity: true),
        "{log_record}",
        "The maximum number of log records the queue of a given instance of an SDK log processor can hold.");

    internal static readonly ObservableUpDownCounter<long> SpanQueueSize = Meter.CreateObservableUpDownCounter(
        "otel.sdk.processor.span.queue.size",
        () => ObserveProcessorQueues(SpanProcessorQueueRegistrations, observeCapacity: false),
        "{span}",
        "The number of spans in the queue of a given instance of an SDK span processor.");

    internal static readonly ObservableUpDownCounter<long> SpanQueueCapacity = Meter.CreateObservableUpDownCounter(
        "otel.sdk.processor.span.queue.capacity",
        () => ObserveProcessorQueues(SpanProcessorQueueRegistrations, observeCapacity: true),
        "{span}",
        "The maximum number of spans the queue of a given instance of an SDK span processor can hold.");

    internal static IDisposable RegisterProcessorQueue(
        bool isLogProcessor,
        Func<long> observeSize,
        long capacity,
        KeyValuePair<string, object?>[] tags)
    {
        var registrations = isLogProcessor
            ? LogProcessorQueueRegistrations
            : SpanProcessorQueueRegistrations;
        var registration = new ProcessorQueueRegistration(registrations, observeSize, capacity, tags);

        lock (ProcessorQueueRegistrationsLock)
        {
            registrations.Add(registration);
        }

        return registration;
    }

    internal static Measurement<long>[] ObserveProcessorQueues(
        List<ProcessorQueueRegistration> registrations,
        bool observeCapacity)
    {
        lock (ProcessorQueueRegistrationsLock)
        {
            var measurements = new Measurement<long>[registrations.Count];

            for (var i = 0; i < registrations.Count; i++)
            {
                var registration = registrations[i];
                var value = observeCapacity ? registration.Capacity : registration.ObserveSize();
                measurements[i] = new(value, registration.Tags);
            }

            return measurements;
        }
    }

    internal sealed class ProcessorQueueRegistration : IDisposable
    {
        private readonly List<ProcessorQueueRegistration> registrations;
        private int disposed;

        public ProcessorQueueRegistration(
            List<ProcessorQueueRegistration> registrations,
            Func<long> observeSize,
            long capacity,
            KeyValuePair<string, object?>[] tags)
        {
            this.registrations = registrations;
            this.ObserveSize = observeSize;
            this.Capacity = capacity;
            this.Tags = tags;
        }

        public Func<long> ObserveSize { get; }

        public long Capacity { get; }

        public KeyValuePair<string, object?>[] Tags { get; }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) != 0)
            {
                return;
            }

            lock (ProcessorQueueRegistrationsLock)
            {
                this.registrations.Remove(this);
            }
        }
    }
}
