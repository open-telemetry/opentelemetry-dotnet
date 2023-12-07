// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Net.Http.Headers;
using Google.Protobuf;
using Grpc.Net.Compression;

namespace OpenTelemetry.Instrumentation.Grpc.Tests.GrpcTestHelpers;

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
