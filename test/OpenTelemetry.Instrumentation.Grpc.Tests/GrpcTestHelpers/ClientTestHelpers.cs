// <copyright file="ClientTestHelpers.cs" company="OpenTelemetry Authors">
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
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Net.Compression;

namespace OpenTelemetry.Instrumentation.Grpc.Tests.GrpcTestHelpers
{
    internal static class ClientTestHelpers
    {
        public static HttpClient CreateTestClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> sendAsync, Uri baseAddress = null)
        {
            var handler = TestHttpMessageHandler.Create(sendAsync);
            var httpClient = new HttpClient(handler);
            httpClient.BaseAddress = baseAddress ?? new Uri("https://localhost");

            return httpClient;
        }

        public static Task<StreamContent> CreateResponseContent<TResponse>(TResponse response, ICompressionProvider compressionProvider = null)
            where TResponse : IMessage<TResponse>
        {
            return CreateResponseContentCore(new[] { response }, compressionProvider);
        }

        public static async Task WriteResponseAsync<TResponse>(Stream ms, TResponse response, ICompressionProvider compressionProvider)
            where TResponse : IMessage<TResponse>
        {
            var compress = false;

            byte[] data;
            if (compressionProvider != null)
            {
                compress = true;

                var output = new MemoryStream();
                var compressionStream = compressionProvider.CreateCompressionStream(output, System.IO.Compression.CompressionLevel.Fastest);
                var compressedData = response.ToByteArray();

                compressionStream.Write(compressedData, 0, compressedData.Length);
                compressionStream.Flush();
                compressionStream.Dispose();
                data = output.ToArray();
            }
            else
            {
                data = response.ToByteArray();
            }

            await ResponseUtils.WriteHeaderAsync(ms, data.Length, compress, CancellationToken.None);
#if NET5_0_OR_GREATER
            await ms.WriteAsync(data);
#else
            await ms.WriteAsync(data, 0, data.Length);
#endif
        }

        private static async Task<StreamContent> CreateResponseContentCore<TResponse>(TResponse[] responses, ICompressionProvider compressionProvider)
            where TResponse : IMessage<TResponse>
        {
            var ms = new MemoryStream();
            foreach (var response in responses)
            {
                await WriteResponseAsync(ms, response, compressionProvider);
            }

            ms.Seek(0, SeekOrigin.Begin);
            var streamContent = new StreamContent(ms);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/grpc");
            return streamContent;
        }
    }
}
