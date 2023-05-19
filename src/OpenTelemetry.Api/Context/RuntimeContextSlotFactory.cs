// <copyright file="RuntimeContextSlotFactory.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics.CodeAnalysis;

namespace OpenTelemetry.Context
{
    internal abstract class RuntimeContextSlotFactory
    {
        public abstract RuntimeContextSlot<T> Create<T>(string name);

        public sealed class AsyncLocalRuntimeContextSlotFactory : RuntimeContextSlotFactory
        {
            public override RuntimeContextSlot<T> Create<T>(string name)
                => new AsyncLocalRuntimeContextSlot<T>(name);
        }

#if NETFRAMEWORK
        public sealed class RemotingRuntimeContextSlotFactory : RuntimeContextSlotFactory
        {
            public override RuntimeContextSlot<T> Create<T>(string name)
                => new RemotingRuntimeContextSlot<T>(name);
        }
#endif

        public sealed class ThreadLocalRuntimeContextSlotFactory : RuntimeContextSlotFactory
        {
            public override RuntimeContextSlot<T> Create<T>(string name)
                => new ThreadLocalRuntimeContextSlot<T>(name);
        }

        [RequiresUnreferencedCode("ReflectionRuntimeContextSlotFactory is trimmer unsafe.")]
        [RequiresDynamicCode("ReflectionRuntimeContextSlotFactory requires the ability to generate new code at runtime.")]
        public sealed class ReflectionRuntimeContextSlotFactory : RuntimeContextSlotFactory
        {
            private readonly Type runtimeContextSlotType;

            public ReflectionRuntimeContextSlotFactory(Type runtimeContextSlotType)
            {
                this.runtimeContextSlotType = runtimeContextSlotType;
            }

            public override RuntimeContextSlot<T> Create<T>(string name)
            {
                var type = this.runtimeContextSlotType.MakeGenericType(typeof(T));
                var ctor = type.GetConstructor(new Type[] { typeof(string) })!;
                return (RuntimeContextSlot<T>)ctor.Invoke(new object[] { name })!;
            }
        }
    }
}
