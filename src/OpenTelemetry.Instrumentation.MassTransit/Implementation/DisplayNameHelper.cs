// <copyright file="DisplayNameHelper.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Instrumentation.MassTransit.Implementation
{
    internal static class DisplayNameHelper
    {
        private static readonly ConcurrentDictionary<string, string> SendOperationDisplayNameCache = new ConcurrentDictionary<string, string>();
        private static readonly ConcurrentDictionary<string, string> ReceiveOperationDisplayNameCache = new ConcurrentDictionary<string, string>();
        private static readonly ConcurrentDictionary<string, string> ConsumeOperationDisplayNameCache = new ConcurrentDictionary<string, string>();
        private static readonly ConcurrentDictionary<string, string> HandleOperationDisplayNameCache = new ConcurrentDictionary<string, string>();

        public static string GetSendOperationDisplayName(string peerAddress) =>
            SendOperationDisplayNameCache.GetOrAdd(peerAddress, ConvertSendOperationToDisplayName);

        public static string GetReceiveOperationDisplayName(string peerAddress) =>
            ReceiveOperationDisplayNameCache.GetOrAdd(peerAddress, ConvertReceiveOperationToDisplayName);

        public static string GetConsumeOperationDisplayName(string peerAddress) =>
            ConsumeOperationDisplayNameCache.GetOrAdd(peerAddress, ConvertConsumeOperationToDisplayName);

        public static string GetHandleOperationDisplayName(string peerAddress) =>
            HandleOperationDisplayNameCache.GetOrAdd(peerAddress, ConvertHandleOperationToDisplayName);

        private static string ConvertSendOperationToDisplayName(string peerAddress) => $"SEND {peerAddress}";

        private static string ConvertReceiveOperationToDisplayName(string peerAddress) => $"RECV {peerAddress}";

        private static string ConvertConsumeOperationToDisplayName(string consumerType) => $"CONSUME {consumerType}";

        private static string ConvertHandleOperationToDisplayName(string peerAddress) => $"HANDLE {peerAddress}";
    }
}
