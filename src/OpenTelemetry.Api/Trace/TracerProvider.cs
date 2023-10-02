// <copyright file="TracerProvider.cs" company="OpenTelemetry Authors">
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

#nullable enable

using System.Collections;

namespace OpenTelemetry.Trace;

/// <summary>
/// TracerProvider is the entry point of the OpenTelemetry API. It provides access to <see cref="Tracer"/>.
/// </summary>
public class TracerProvider : BaseProvider
{
    private Hashtable? tracers = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TracerProvider"/> class.
    /// </summary>
    protected TracerProvider()
    {
    }

    /// <summary>
    /// Gets the default <see cref="TracerProvider"/>.
    /// </summary>
    public static TracerProvider Default { get; } = new TracerProvider();

    /// <summary>
    /// Gets a tracer with given name and version.
    /// </summary>
    /// <param name="name">Name identifying the instrumentation library.</param>
    /// <param name="version">Version of the instrumentation library.</param>
    /// <returns>Tracer instance.</returns>
    public Tracer GetTracer(string name, string? version = null)
    {
        var tracers = this.tracers
            ?? throw new ObjectDisposedException(nameof(TracerProvider));

        var key = new TracerKey(name, version);

        if (tracers[key] is not Tracer tracer)
        {
            lock (tracers)
            {
                tracer = (tracers[key] as Tracer)!;
                if (tracer == null)
                {
                    if (this.tracers != null)
                    {
                        tracer = new(new(key.Name, key.Version));
                        tracers[key] = tracer;
                    }
                    else
                    {
                        tracer = new(activitySource: null);
                    }
                }
            }
        }

        return tracer;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            var tracers = Interlocked.CompareExchange(ref this.tracers, null, this.tracers);
            if (tracers != null)
            {
                lock (tracers)
                {
                    foreach (DictionaryEntry entry in tracers)
                    {
                        var tracer = (Tracer)entry.Value!;
                        var activitySource = tracer.ActivitySource;
                        tracer.ActivitySource = null;
                        activitySource?.Dispose();
                    }

                    tracers.Clear();
                }
            }
        }

        base.Dispose(disposing);
    }

    private sealed record class TracerKey
    {
        public TracerKey(string? name, string? version)
        {
            this.Name = name ?? string.Empty;
            this.Version = version;
        }

        public string Name { get; }

        public string? Version { get; }
    }
}
