// <copyright file="ExceptionProcessor.cs" company="OpenTelemetry Authors">
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
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;

namespace OpenTelemetry.Trace
{
    internal sealed class ExceptionProcessor : BaseProcessor<Activity>
    {
        private const string ExceptionPointersKey = "otel.exception_pointers";

        private readonly Func<IntPtr> fnGetExceptionPointers;

        public ExceptionProcessor()
        {
            try
            {
                var flags = BindingFlags.Static | BindingFlags.Public;
                var method = typeof(Marshal).GetMethod("GetExceptionPointers", flags, null, new Type[] { }, null);
                var lambda = Expression.Lambda<Func<IntPtr>>(Expression.Call(method));
                this.fnGetExceptionPointers = lambda.Compile();
            }
            catch (Exception ex)
            {
                throw new NotSupportedException($"'{typeof(Marshal).FullName}.GetExceptionPointers' is not supported", ex);
            }
        }

        /// <inheritdoc />
        public override void OnStart(Activity activity)
        {
            var pointers = this.fnGetExceptionPointers();

            if (pointers != IntPtr.Zero)
            {
                activity.SetTag(ExceptionPointersKey, pointers);
            }
        }

        /// <inheritdoc />
        public override void OnEnd(Activity activity)
        {
            var pointers = this.fnGetExceptionPointers();

            if (pointers == IntPtr.Zero)
            {
                return;
            }

            var snapshot = activity.GetTagValue(ExceptionPointersKey) as IntPtr?;

            if (snapshot != null)
            {
                activity.SetTag(ExceptionPointersKey, null);
            }

            if (snapshot != pointers)
            {
                activity.SetStatus(Status.Error);
            }
        }
    }
}
