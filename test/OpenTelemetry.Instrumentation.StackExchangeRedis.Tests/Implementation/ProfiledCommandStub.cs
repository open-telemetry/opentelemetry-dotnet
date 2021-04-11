// <copyright file="ProfiledCommandStub.cs" company="OpenTelemetry Authors">
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
using System.Net;
using StackExchange.Redis;
using StackExchange.Redis.Profiling;

namespace OpenTelemetry.Instrumentation.StackExchangeRedis.Implementation
{
    internal sealed class ProfiledCommandStub : IProfiledCommand
    {
        internal Message Message;

        public ProfiledCommandStub(
            int db,
            string command,
            string message)
        {
            this.Db = db;
            this.Command = command;
            this.Message = new Message(message);
        }

        /// <inheritdoc/>
        public EndPoint EndPoint => null;

        /// <inheritdoc/>
        public int Db { get; }

        /// <inheritdoc/>
        public string Command { get; }

        /// <inheritdoc/>
        public CommandFlags Flags => CommandFlags.None;

        /// <inheritdoc/>
        public DateTime CommandCreated => DateTime.UtcNow;

        /// <inheritdoc/>
        public TimeSpan CreationToEnqueued => TimeSpan.Zero;

        /// <inheritdoc/>
        public TimeSpan EnqueuedToSending => TimeSpan.Zero;

        /// <inheritdoc/>
        public TimeSpan SentToResponse => TimeSpan.Zero;

        /// <inheritdoc/>
        public TimeSpan ResponseToCompletion => TimeSpan.Zero;

        /// <inheritdoc/>
        public TimeSpan ElapsedTime => TimeSpan.Zero;

        /// <inheritdoc/>
        public IProfiledCommand RetransmissionOf => null;

        /// <inheritdoc/>
        public RetransmissionReasonType? RetransmissionReason => null;
    }
}
