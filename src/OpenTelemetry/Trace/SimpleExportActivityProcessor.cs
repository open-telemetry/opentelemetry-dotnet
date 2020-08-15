﻿// <copyright file="SimpleExportActivityProcessor.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Implements activity processor that exports <see cref="Activity"/> at each OnEnd call.
    /// </summary>
    public class SimpleExportActivityProcessor : ReentrantExportActivityProcessor
    {
        private readonly object lck = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleExportActivityProcessor"/> class.
        /// </summary>
        /// <param name="exporter">Activity exporter instance.</param>
        public SimpleExportActivityProcessor(ActivityExporterSync exporter)
            : base(exporter)
        {
        }

        /// <inheritdoc />
        public override void OnEnd(Activity activity)
        {
            lock (this.lck)
            {
                base.OnEnd(activity);
            }
        }
    }
}
