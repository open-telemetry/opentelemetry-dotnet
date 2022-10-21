// <copyright file="TracerProviderBuilderState.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Stores state used to build a <see cref="TracerProvider"/>.
    /// </summary>
    internal sealed class TracerProviderBuilderState
    {
        internal readonly IServiceProvider ServiceProvider;
        internal readonly List<InstrumentationRegistration> Instrumentation = new();
        internal readonly List<BaseProcessor<Activity>> Processors = new();
        internal readonly List<string> Sources = new();
        internal readonly HashSet<string> LegacyActivityOperationNames = new(StringComparer.OrdinalIgnoreCase);
        internal ResourceBuilder? ResourceBuilder;
        internal Sampler? Sampler;
        internal bool SetErrorStatusOnException;

        private bool hasEnteredBuildPhase;
        private TracerProviderBuilderSdk? builder;

        public TracerProviderBuilderState(IServiceProvider serviceProvider)
        {
            Debug.Assert(serviceProvider != null, "serviceProvider was null");

            this.ServiceProvider = serviceProvider!;
        }

        public TracerProviderBuilderSdk Builder => this.builder ??= new TracerProviderBuilderSdk(this);

        public void CheckForCircularBuild()
        {
            if (this.hasEnteredBuildPhase)
            {
                throw new NotSupportedException("TracerProvider cannot be accessed while build is executing.");
            }

            this.hasEnteredBuildPhase = true;
        }

        public void AddInstrumentation(
            string instrumentationName,
            string instrumentationVersion,
            object instrumentation)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(instrumentationName), "instrumentationName was null or whitespace");
            Debug.Assert(!string.IsNullOrWhiteSpace(instrumentationVersion), "instrumentationVersion was null or whitespace");
            Debug.Assert(instrumentation != null, "instrumentation was null");

            this.Instrumentation.Add(
                new InstrumentationRegistration(
                    instrumentationName,
                    instrumentationVersion,
                    instrumentation!));
        }

        public void AddLegacySource(string operationName)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(operationName), "operationName was null or whitespace");

            this.LegacyActivityOperationNames.Add(operationName);
        }

        public void AddProcessor(BaseProcessor<Activity> processor)
        {
            Debug.Assert(processor != null, "processor was null");

            this.Processors.Add(processor!);
        }

        public void AddSource(params string[] names)
        {
            Debug.Assert(names != null, "names was null");

            foreach (var name in names!)
            {
                Guard.ThrowIfNullOrWhitespace(name);

                // TODO: We need to fix the listening model.
                // Today it ignores version.
                this.Sources.Add(name);
            }
        }

        public void ConfigureResource(Action<ResourceBuilder> configure)
        {
            Debug.Assert(configure != null, "configure was null");

            var resourceBuilder = this.ResourceBuilder ??= ResourceBuilder.CreateDefault();

            configure!(resourceBuilder);
        }

        public void SetResourceBuilder(ResourceBuilder resourceBuilder)
        {
            Debug.Assert(resourceBuilder != null, "resourceBuilder was null");

            this.ResourceBuilder = resourceBuilder;
        }

        public void SetSampler(Sampler sampler)
        {
            Debug.Assert(sampler != null, "sampler was null");

            this.Sampler = sampler;
        }

        internal void EnableErrorStatusOnException()
        {
            try
            {
                this.Processors.Insert(0, new ExceptionProcessor());
            }
            catch (Exception ex)
            {
                throw new NotSupportedException($"'{nameof(TracerProviderBuilderExtensions.SetErrorStatusOnException)}' is not supported on this platform", ex);
            }
        }

        internal readonly struct InstrumentationRegistration
        {
            public readonly string Name;
            public readonly string Version;
            public readonly object Instance;

            internal InstrumentationRegistration(string name, string version, object instance)
            {
                this.Name = name;
                this.Version = version;
                this.Instance = instance;
            }
        }
    }
}
