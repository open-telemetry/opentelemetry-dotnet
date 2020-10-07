// <copyright file="OpenTelemetryLoggerOptions.cs" company="OpenTelemetry Authors">
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

#if NETSTANDARD2_0
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Logs
{
    public class OpenTelemetryLoggerOptions
    {
        internal readonly List<LogProcessor> Processors = new List<LogProcessor>();

        /// <summary>
        /// Adds processor to the options.
        /// </summary>
        /// <param name="processor">Log processor to add.</param>
        /// <returns>Returns <see cref="OpenTelemetryLoggerOptions"/> for chaining.</returns>
        public OpenTelemetryLoggerOptions AddProcessor(LogProcessor processor)
        {
            if (processor == null)
            {
                throw new ArgumentNullException(nameof(processor));
            }

            this.Processors.Add(processor);

            return this;
        }
    }
}
#endif
