// <copyright file="MeterProviderBuilder.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// MeterProviderBuilder base class.
    /// </summary>
    public abstract class MeterProviderBuilder
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MeterProviderBuilder"/> class.
        /// </summary>
        protected MeterProviderBuilder()
        {
        }

        /// <summary>
        /// Adds given meter source names to the list of subscribed sources.
        /// </summary>
        /// <param name="names">Meter source names.</param>
        /// <returns>Returns <see cref="MeterProviderBuilder"/> for chaining.</returns>
        public abstract MeterProviderBuilder AddSource(params string[] names);
    }
}
