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

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Context
{
    /// <summary>
    /// Generic runtime context management API.
    /// </summary>
    public static class RuntimeContext
    {
        private static readonly ConcurrentDictionary<string, object> Slots = new();

        private static Type contextSlotType;

        private static RuntimeContextSlotFactory runtimeContextSlotFactory;

        /// <summary>
        /// Gets or sets the actual context carrier implementation.
        /// </summary>
        public static Type ContextSlotType
        {
            get => contextSlotType ?? typeof(AsyncLocalRuntimeContextSlot<>);

            [RequiresDynamicCode("Use 'MethodFriendlyToAot' instead")]
            [RequiresUnreferencedCode("Message")]
            set
            {
                Guard.ThrowIfNull(value, nameof(value));

                if (!value.IsGenericType || !value.IsGenericTypeDefinition || value.GetGenericArguments().Length != 1)
                {
                    throw new NotSupportedException($"Type '{value}' must be generic with a single generic type argument");
                }

                if (value == typeof(AsyncLocalRuntimeContextSlot<>))
                {
                    runtimeContextSlotFactory = new RuntimeContextSlotFactory.AsyncLocalRuntimeContextSlotFactory();
                }
                else if (value == typeof(ThreadLocalRuntimeContextSlot<>))
                {
                    runtimeContextSlotFactory = new RuntimeContextSlotFactory.ThreadLocalRuntimeContextSlotFactory();
                }
#if NETFRAMEWORK
                else if (value == typeof(RemotingRuntimeContextSlot<>))
                {
                    runtimeContextSlotFactory = new RuntimeContextSlotFactory.RemotingRuntimeContextSlotFactory();
                }
#endif
                else
                {
                    runtimeContextSlotFactory = new RuntimeContextSlotFactory.ReflectionContextSlotFactory(contextSlotType);
                    //throw new NotSupportedException("${value} is not supported.");
                }

                contextSlotType = value;
            }
        }

        /// <summary>
        /// Register a named context slot.
        /// </summary>
        /// <param name="slotName">The name of the context slot.</param>
        /// <typeparam name="T">The type of the underlying value.</typeparam>
        /// <returns>The slot registered.</returns>
        public static RuntimeContextSlot<T> RegisterSlot<T>(string slotName)
        {
            Guard.ThrowIfNullOrEmpty(slotName);

            lock (Slots)
            {
                if (Slots.ContainsKey(slotName))
                {
                    throw new InvalidOperationException($"Context slot already registered: '{slotName}'");
                }

                var slot = runtimeContextSlotFactory.Create<T>(slotName);

                Slots[slotName] = slot;
                return slot;
            }
        }

        /// <summary>
        /// Get a registered slot from a given name.
        /// </summary>
        /// <param name="slotName">The name of the context slot.</param>
        /// <typeparam name="T">The type of the underlying value.</typeparam>
        /// <returns>The slot previously registered.</returns>
        public static RuntimeContextSlot<T> GetSlot<T>(string slotName)
        {
            Guard.ThrowIfNullOrEmpty(slotName);
            var slot = GuardNotFound(slotName);
            var contextSlot = Guard.ThrowIfNotOfType<RuntimeContextSlot<T>>(slot);
            return contextSlot;
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
        /// <param name="slotName">The name of the context slot.</param>
        /// <param name="value">The value to be set.</param>
        /// <typeparam name="T">The type of the value.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetValue<T>(string slotName, T value)
        {
            GetSlot<T>(slotName).Set(value);
        }

        /// <summary>
        /// Gets the value from a registered slot.
        /// </summary>
        /// <param name="slotName">The name of the context slot.</param>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <returns>The value retrieved from the context slot.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetValue<T>(string slotName)
        {
            return GetSlot<T>(slotName).Get();
        }

        /// <summary>
        /// Sets the value to a registered slot.
        /// </summary>
        /// <param name="slotName">The name of the context slot.</param>
        /// <param name="value">The value to be set.</param>
        public static void SetValue(string slotName, object value)
        {
            Guard.ThrowIfNullOrEmpty(slotName);
            var slot = GuardNotFound(slotName);
            var runtimeContextSlotValueAccessor = Guard.ThrowIfNotOfType<IRuntimeContextSlotValueAccessor>(slot);
            runtimeContextSlotValueAccessor.Value = value;
        }

        /// <summary>
        /// Gets the value from a registered slot.
        /// </summary>
        /// <param name="slotName">The name of the context slot.</param>
        /// <returns>The value retrieved from the context slot.</returns>
        public static object GetValue(string slotName)
        {
            Guard.ThrowIfNullOrEmpty(slotName);
            var slot = GuardNotFound(slotName);
            var runtimeContextSlotValueAccessor = Guard.ThrowIfNotOfType<IRuntimeContextSlotValueAccessor>(slot);
            return runtimeContextSlotValueAccessor.Value;
        }

        // For testing purpose
        internal static void Clear()
        {
            Slots.Clear();
        }

        private static object GuardNotFound(string slotName)
        {
            if (!Slots.TryGetValue(slotName, out var slot))
            {
                throw new ArgumentException($"Context slot not found: '{slotName}'");
            }

            return slot;
        }
    }
}
