// <copyright file="TracerProviderOptions.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Trace
{
    public class TracerProviderSdkOptions
    {
        public Func<Resource> ResourceFactory { get; set; }

        public ICollection<string> Sources { get; } = new List<string>();

        public ICollection<InstrumentationFactory> InstrumentationFactories { get; } = new List<InstrumentationFactory>();

        public Func<Sampler> SamplerFactory { get; set; }

        public ICollection<Func<BaseProcessor<Activity>>> ProcessorFactories { get; } = new List<Func<BaseProcessor<Activity>>>();

        public IDictionary<string, bool> LegacyActivityOperationNames { get; } = new Dictionary<string, bool>();

        public bool SetErrorStatusOnException { get; set; } = false;

        public readonly struct InstrumentationFactory
        {
            public readonly string Name;
            public readonly string Version;
            public readonly Func<object> Factory;

            public InstrumentationFactory(string name, string version, Func<object> factory)
            {
                this.Name = name;
                this.Version = version;
                this.Factory = factory;
            }
        }
    }
}
