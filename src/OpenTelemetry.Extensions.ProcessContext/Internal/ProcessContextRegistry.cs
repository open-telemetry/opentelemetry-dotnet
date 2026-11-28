// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Extensions.ProcessContext.Internal;

/// <summary>
/// Tracks the providers which publish the process context. There is only one
/// context per process, so the most recently built provider which is still
/// alive wins; when it shuts down the previous one is published again, and
/// when the last one shuts down the context is dropped.
/// </summary>
internal sealed class ProcessContextRegistry
{
    private readonly Lock syncRoot = new();
    private readonly List<ProcessContextRegistration> active = [];
    private readonly ProcessContextWriter writer;

    internal ProcessContextRegistry(ProcessContextWriter writer)
    {
        this.writer = writer;
    }

    public static ProcessContextRegistry Default { get; } = new(ProcessContextWriter.Default);

    internal ProcessContextWriter Writer => this.writer;

    internal void Add(ProcessContextRegistration registration)
    {
        lock (this.syncRoot)
        {
            this.active.Add(registration);
            this.writer.Publish(registration.Payload!);
        }
    }

    internal void Remove(ProcessContextRegistration registration)
    {
        lock (this.syncRoot)
        {
            if (!this.active.Remove(registration))
            {
                return;
            }

            if (this.active.Count == 0)
            {
                this.writer.Drop();
            }
            else
            {
                this.writer.Publish(this.active[this.active.Count - 1].Payload!);
            }
        }
    }
}
