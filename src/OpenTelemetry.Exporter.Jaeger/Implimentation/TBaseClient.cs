// <copyright file="TBaseClient.cs" company="OpenTelemetry Authors">
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

#if NET46

namespace OpenTelemetry.Exporter.Jaeger.Implimentation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Thrift.Protocol;

    public abstract class TBaseClient
    {
        public readonly Guid ClientId = Guid.NewGuid();

        private readonly TProtocol inputProtocol;
        private readonly TProtocol outputProtocol;

        private bool isDisposed;
        private int seqId;

        protected TBaseClient(TProtocol inputProtocol, TProtocol outputProtocol)
        {
            this.inputProtocol = inputProtocol ?? throw new ArgumentNullException(nameof(inputProtocol));
            this.outputProtocol = outputProtocol ?? throw new ArgumentNullException(nameof(outputProtocol));
        }

        public TProtocol InputProtocol => this.inputProtocol;

        public TProtocol OutputProtocol => this.outputProtocol;

        public int SeqId
        {
            get { return ++this.seqId; }
        }

        public virtual void OpenTransport()
        {
            if (!this.inputProtocol.Transport.IsOpen)
            {
                this.inputProtocol.Transport.Open();
            }

            if (!this.inputProtocol.Transport.IsOpen)
            {
                this.outputProtocol.Transport.Open();
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                if (disposing)
                {
                    this.inputProtocol?.Dispose();
                    this.outputProtocol?.Dispose();
                }
            }

            this.isDisposed = true;
        }
    }
}

#endif
