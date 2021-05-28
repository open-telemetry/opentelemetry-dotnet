// <copyright file="DeferredServiceCollection.cs" company="OpenTelemetry Authors">
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
using System.Collections;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace OpenTelemetry
{
    internal sealed class DeferredServiceCollection : IDeferredServiceCollection
    {
        public DeferredServiceCollection(IServiceCollection services)
        {
            this.Services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public IServiceCollection Services { get; }

        public int Count => this.Services.Count;

        public bool IsReadOnly => this.Services.IsReadOnly;

        public DeferredServiceDescriptor this[int index]
        {
            get => ConvertToOtel(this.Services[index]);
            set => this.Services[index] = ConvertToMs(value);
        }

        public int IndexOf(DeferredServiceDescriptor item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            for (int i = 0; i < this.Services.Count; i++)
            {
                var msRecord = this.Services[i];

                if (msRecord.ServiceType == item.ServiceType
                    && msRecord.ImplementationType == item.ImplementationType
                    && msRecord.ImplementationInstance == item.ImplementationInstance
                    && msRecord.ImplementationFactory == item.ImplementationFactory
                    && ConvertToOtel(msRecord.Lifetime) == item.Lifetime)
                {
                    return i;
                }
            }

            return -1;
        }

        public void Insert(int index, DeferredServiceDescriptor item)
        {
            this.Services.Insert(index, ConvertToMs(item));
        }

        public void RemoveAt(int index)
        {
            this.Services.RemoveAt(index);
        }

        public void Add(DeferredServiceDescriptor item)
        {
            this.Services.Add(ConvertToMs(item));
        }

        public void Clear()
        {
            this.Services.Clear();
        }

        public bool Contains(DeferredServiceDescriptor item)
        {
            return this.IndexOf(item) >= 0;
        }

        public void CopyTo(DeferredServiceDescriptor[] array, int arrayIndex)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            var msRecords = new ServiceDescriptor[array.Length];

            this.Services.CopyTo(msRecords, arrayIndex);

            for (int i = arrayIndex; i < array.Length; i++)
            {
                array[i] = ConvertToOtel(msRecords[i]);
            }
        }

        public bool Remove(DeferredServiceDescriptor item)
        {
            int index = this.IndexOf(item);

            if (index >= 0)
            {
                this.Services.RemoveAt(index);
                return true;
            }

            return false;
        }

        public IEnumerator<DeferredServiceDescriptor> GetEnumerator()
        {
            foreach (ServiceDescriptor serviceDescriptor in this.Services)
            {
                yield return ConvertToOtel(serviceDescriptor);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        private static DeferredServiceDescriptor ConvertToOtel(ServiceDescriptor serviceDescriptor)
        {
            if (serviceDescriptor == null)
            {
                return null;
            }

            if (serviceDescriptor.ImplementationInstance != null)
            {
                return new DeferredServiceDescriptor(serviceDescriptor.ServiceType, serviceDescriptor.ImplementationInstance);
            }

            var lifetime = ConvertToOtel(serviceDescriptor.Lifetime);

            if (serviceDescriptor.ImplementationFactory != null)
            {
                return new DeferredServiceDescriptor(serviceDescriptor.ServiceType, serviceDescriptor.ImplementationFactory, lifetime);
            }

            return new DeferredServiceDescriptor(serviceDescriptor.ServiceType, serviceDescriptor.ImplementationType, lifetime);
        }

        private static DeferredServiceLifetime ConvertToOtel(ServiceLifetime serviceLifetime)
        {
            return serviceLifetime switch
            {
                ServiceLifetime.Singleton => DeferredServiceLifetime.Singleton,
                ServiceLifetime.Scoped => DeferredServiceLifetime.Scoped,
                ServiceLifetime.Transient => DeferredServiceLifetime.Transient,
                _ => throw new NotSupportedException($"ServiceLifetime '{serviceLifetime}' is not supported."),
            };
        }

        private static ServiceDescriptor ConvertToMs(DeferredServiceDescriptor serviceDescriptor)
        {
            if (serviceDescriptor == null)
            {
                return null;
            }

            if (serviceDescriptor.ImplementationInstance != null)
            {
                return new ServiceDescriptor(serviceDescriptor.ServiceType, serviceDescriptor.ImplementationInstance);
            }

            var lifetime = ConvertToMs(serviceDescriptor.Lifetime);

            if (serviceDescriptor.ImplementationFactory != null)
            {
                return new ServiceDescriptor(serviceDescriptor.ServiceType, serviceDescriptor.ImplementationFactory, lifetime);
            }

            return new ServiceDescriptor(serviceDescriptor.ServiceType, serviceDescriptor.ImplementationType, lifetime);
        }

        private static ServiceLifetime ConvertToMs(DeferredServiceLifetime serviceLifetime)
        {
            return serviceLifetime switch
            {
                DeferredServiceLifetime.Singleton => ServiceLifetime.Singleton,
                DeferredServiceLifetime.Scoped => ServiceLifetime.Scoped,
                DeferredServiceLifetime.Transient => ServiceLifetime.Transient,
                _ => throw new NotSupportedException($"ServiceLifetime '{serviceLifetime}' is not supported."),
            };
        }
    }
}
