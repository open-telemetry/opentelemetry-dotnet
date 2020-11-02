// <copyright file="RemotingRuntimeContextSlot.cs" company="OpenTelemetry Authors">
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

#if NET452
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Messaging;

namespace OpenTelemetry.Context
{
    /// <summary>
    /// The .NET Remoting implementation of context slot.
    /// </summary>
    /// <typeparam name="T">The type of the underlying value.</typeparam>
    public class RemotingRuntimeContextSlot<T> : RuntimeContextSlot<T>
    {
        // A special workaround to suppress context propagation cross AppDomains.
        //
        // By default the value added to System.Runtime.Remoting.Messaging.CallContext
        // will be marshalled/unmarshalled across AppDomain boundary. This will cause
        // serious issue if the destination AppDomain doesn't have the corresponding type
        // to unmarshal data.
        // The worst case is AppDomain crash with ReflectionLoadTypeException.
        //
        // The workaround is to use a well known type that exists in all AppDomains, and
        // put the actual payload as a non-public field so the field is ignored during
        // marshalling.
        private static readonly FieldInfo WrapperField = typeof(BitArray).GetField("_syncRoot", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// Initializes a new instance of the <see cref="RemotingRuntimeContextSlot{T}"/> class.
        /// </summary>
        /// <param name="name">The name of the context slot.</param>
        public RemotingRuntimeContextSlot(string name)
            : base(name)
        {
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override T Get()
        {
            var wrapper = CallContext.LogicalGetData(this.Name) as BitArray;

            if (wrapper == null)
            {
                return default(T);
            }

            var value = WrapperField.GetValue(wrapper);
            if (value is T)
            {
                return (T)value;
            }

            return default(T);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Set(T value)
        {
            var wrapper = new BitArray(0);
            WrapperField.SetValue(wrapper, value);
            CallContext.LogicalSetData(this.Name, wrapper);
        }
    }
}
#endif
