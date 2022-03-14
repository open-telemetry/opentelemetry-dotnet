// <copyright file="LogConvertingProcessor.cs" company="OpenTelemetry Authors">
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

using System.Threading;

namespace OpenTelemetry.Logs
{
    internal sealed class LogConvertingProcessor<T> : ILogProcessor
        where T : class
    {
        private readonly LogConverter<T> logConverter;
        private readonly BaseProcessor<T> innerProcessor;

        public LogConvertingProcessor(
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

        public void OnEnd(in LogRecordStruct log)
        {
            T record;
            try
            {
                record = this.logConverter(in log);
            }
            catch
            {
                // TODO: Log event, exception thrown converting log.
                return;
            }

            if (record != null)
            {
                this.innerProcessor.OnEnd(record);
            }
        }

        public bool ForceFlush(int timeoutMilliseconds = Timeout.Infinite)
            => this.innerProcessor.ForceFlush(timeoutMilliseconds);

        public bool Shutdown(int timeoutMilliseconds = Timeout.Infinite)
            => this.innerProcessor.Shutdown(timeoutMilliseconds);

        public void Dispose()
            => this.innerProcessor.Dispose();
    }
}
