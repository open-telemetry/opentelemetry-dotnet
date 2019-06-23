﻿// <copyright file="RunningSpanStoreSummary.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

namespace OpenTelemetry.Trace.Export
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;

    public sealed class RunningSpanStoreSummary : IRunningSpanStoreSummary
    {
        internal RunningSpanStoreSummary(IDictionary<string, IRunningPerSpanNameSummary> perSpanNameSummary)
        {
            this.PerSpanNameSummary = perSpanNameSummary;
        }

        public IDictionary<string, IRunningPerSpanNameSummary> PerSpanNameSummary { get; }

        public static IRunningSpanStoreSummary Create(IDictionary<string, IRunningPerSpanNameSummary> perSpanNameSummary)
        {
            if (perSpanNameSummary == null)
            {
                throw new ArgumentNullException(nameof(perSpanNameSummary));
            }

            IDictionary<string, IRunningPerSpanNameSummary> copy = new Dictionary<string, IRunningPerSpanNameSummary>(perSpanNameSummary);
            return new RunningSpanStoreSummary(new ReadOnlyDictionary<string, IRunningPerSpanNameSummary>(copy));
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "RunningSummary{"
                + "perSpanNameSummary=" + this.PerSpanNameSummary
                + "}";
        }

    /// <inheritdoc/>
        public override bool Equals(object o)
        {
            if (o == this)
            {
                return true;
            }

            if (o is RunningSpanStoreSummary that)
            {
                return this.PerSpanNameSummary.SequenceEqual(that.PerSpanNameSummary);
            }

            return false;
        }

    /// <inheritdoc/>
        public override int GetHashCode()
        {
            var h = 1;
            h *= 1000003;
            h ^= this.PerSpanNameSummary.GetHashCode();
            return h;
        }
    }
}
