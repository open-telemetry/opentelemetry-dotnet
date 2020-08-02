// <copyright file="RuntimeContext.cs" company="OpenTelemetry Authors">
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace OpenTelemetry.Context
{
    /// <summary>
    /// Generic runtime context management API.
    /// </summary>
    public sealed class RuntimeContext
    {
        private static readonly ConcurrentDictionary<string, object> Slots = new ConcurrentDictionary<string, object>();

        /// <summary>
        /// Gets or sets the actual context carrier implementation.
        /// </summary>
#if !NET452
        public static Type ContextSlotType { get; set; } = typeof(AsyncLocalRuntimeContextSlot<>);
#else
        public static Type ContextSlotType { get; set; } = typeof(RemotingRuntimeContextSlot<>);
#endif

        /// <summary>
        /// Register a named context slot.
        /// </summary>
        /// <param name="name">The name of the context slot.</param>
        /// <typeparam name="T">The type of the underlying value.</typeparam>
        /// <returns>The slot registered.</returns>
        public static RuntimeContextSlot<T> RegisterSlot<T>(string name)
        {
            lock (Slots)
            {
                if (Slots.ContainsKey(name))
                {
                    throw new InvalidOperationException($"The context slot {name} is already registered.");
                }

                var type = ContextSlotType.MakeGenericType(typeof(T));
                var ctor = type.GetConstructor(new Type[] { typeof(string) });
                var slot = (RuntimeContextSlot<T>)ctor.Invoke(new object[] { name });
                Slots[name] = slot;
                return slot;
            }
        }

        /// <summary>
        /// Get a registered slot from a given name.
        /// </summary>
        /// <param name="name">The name of the context slot.</param>
        /// <typeparam name="T">The type of the underlying value.</typeparam>
        /// <returns>The slot previously registered.</returns>
        public static RuntimeContextSlot<T> GetSlot<T>(string name)
        {
            return (RuntimeContextSlot<T>)Slots[name];
        }

        /*
        public static void Apply(IDictionary<string, object> snapshot)
        {
            foreach (var entry in snapshot)
            {
                // TODO: revisit this part if we want Snapshot() to be used on critical paths
                dynamic value = entry.Value;
                SetValue(entry.Key, value);
            }
        }

        public static IDictionary<string, object> Snapshot()
        {
            var retval = new Dictionary<string, object>();
            foreach (var entry in Slots)
            {
                // TODO: revisit this part if we want Snapshot() to be used on critical paths
                dynamic slot = entry.Value;
                retval[entry.Key] = slot.Get();
            }
            return retval;
        }
        */

        /// <summary>
        /// Sets the value to a registered slot.
        /// </summary>
        /// <param name="name">The name of the context slot.</param>
        /// <param name="value">The value to be set.</param>
        /// <typeparam name="T">The type of the value.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetValue<T>(string name, T value)
        {
            var slot = (RuntimeContextSlot<T>)Slots[name];
            slot.Set(value);
        }

        /// <summary>
        /// Gets the value from a registered slot.
        /// </summary>
        /// <param name="name">The name of the context slot.</param>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <returns>The value retrieved from the context slot.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetValue<T>(string name)
        {
            var slot = (RuntimeContextSlot<T>)Slots[name];
            return slot.Get();
        }

        // For testing purpose
        // private static Clear
    }
}
