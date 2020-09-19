// <copyright file="TracerProviderExtensions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace
{
    public static class TracerProviderExtensions
    {
        public static TracerProvider AddProcessor(this TracerProvider provider, ActivityProcessor processor)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            if (processor == null)
            {
                throw new ArgumentNullException(nameof(processor));
            }

            var trait = provider as TracerProviderSdk;

            if (trait == null)
            {
                throw new ArgumentException($"{nameof(provider)} is not an instance of TracerProviderSdk");
            }

            return trait.AddProcessor(processor);
        }
    }
}
