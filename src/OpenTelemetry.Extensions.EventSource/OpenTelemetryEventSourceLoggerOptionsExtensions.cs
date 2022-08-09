// <copyright file="OpenTelemetryEventSourceLoggerOptionsExtensions.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs
{
    /// <summary>
    /// Contains extension methods for registering OpenTelemetry EventSource utilities into logging services.
    /// </summary>
    public static class OpenTelemetryEventSourceLoggerOptionsExtensions
    {
        /// <summary>
        /// Registers an <see cref="EventListener"/> which will convert <see
        /// cref="EventSource"/> events into OpenTelemetry logs.
        /// </summary>
        /// <param name="options"><see
        /// cref="OpenTelemetryLoggerOptions"/>.</param>
        /// <param name="shouldListenToFunc">Callback function used to decide if
        /// events should be captured for a given <see
        /// cref="EventSource.Name"/>. Return <see langword="null"/> if no
        /// events should be captured.</param>
        /// <returns>Supplied <see cref="OpenTelemetryLoggerOptions"/> for
        /// chaining calls.</returns>
        public static OpenTelemetryLoggerOptions AddEventSourceLogEmitter(
            this OpenTelemetryLoggerOptions options,
            Func<string, EventLevel?> shouldListenToFunc)
        {
            Guard.ThrowIfNull(options);
            Guard.ThrowIfNull(shouldListenToFunc);

            options.ConfigureServices(services => services.TryAddSingleton<EventSourceManager>());

            options.ConfigureProvider((sp, provider) =>
            {
                var manager = sp.GetRequiredService<EventSourceManager>();

                manager.Emitters.Add(
                    new OpenTelemetryEventSourceLogEmitter(provider, shouldListenToFunc, disposeProvider: false));
            });

            return options;
        }

        internal sealed class EventSourceManager : IDisposable
        {
            public List<OpenTelemetryEventSourceLogEmitter> Emitters { get; } = new();

            public void Dispose()
            {
                foreach (var emitter in this.Emitters)
                {
                    emitter.Dispose();
                }

                this.Emitters.Clear();
            }
        }
    }
}
