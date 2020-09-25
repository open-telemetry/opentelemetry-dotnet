// <copyright file="TestActivityExporter.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Tests.Shared
{
    internal class TestActivityExporter : ActivityExporter
    {
        internal readonly BlockingCollection<Activity> Exported = new BlockingCollection<Activity>();
        private bool disposed;

        public override ExportResult Export(in Batch<Activity> batch)
        {
            foreach (var activity in batch)
            {
                this.Exported.Add(activity);
            }

            return ExportResult.Success;
        }

        protected override void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    this.Exported.Dispose();
                }

                this.disposed = true;
            }

            base.Dispose(disposing);
        }
    }
}
