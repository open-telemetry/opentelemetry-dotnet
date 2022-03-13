// <copyright file="InlineLogProcessor{T}.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Logs
{
    internal sealed class InlineLogProcessor<T> : IInlineLogProcessor
        where T : class
    {
        private readonly LogConverter<T> logConverter;
        private readonly BaseProcessor<T> innerProcessor;

        public InlineLogProcessor(
            LogConverter<T> logConverter,
            BaseProcessor<T> innerProcessor)
        {
            this.logConverter = logConverter;
            this.innerProcessor = innerProcessor;
        }

        public void SetParentProvider(BaseProvider parentProvider)
        {
            this.innerProcessor.SetParentProvider(parentProvider);
        }

        public void Log(
            string categoryName,
            DateTime timestamp,
            LogLevel logLevel,
            EventId eventId,
            object state,
            IReadOnlyList<KeyValuePair<string, object>> parsedState,
            IExternalScopeProvider scopeProvider,
            Exception exception,
            string formattedLogMessage)
        {
            T record;
            try
            {
                ActivityContext activityContext = Activity.Current?.Context ?? default;

                record = this.logConverter(
                    in activityContext,
                    categoryName,
                    timestamp,
                    logLevel,
                    eventId,
                    state,
                    parsedState,
                    scopeProvider,
                    exception,
                    formattedLogMessage);
            }
            catch (Exception ex)
            {
                // TODO: Log event, exception thrown converting log.
                return;
            }

            if (record != null)
            {
                this.innerProcessor.OnEnd(record);
            }
        }

        public bool Shutdown(int timeoutMilliseconds = Timeout.Infinite)
            => this.innerProcessor.Shutdown(timeoutMilliseconds);

        public void Dispose()
            => this.innerProcessor.Dispose();
    }
}
