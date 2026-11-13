// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Buffers;
#endif
using System.Text;

namespace System.Net.Http;

internal static class HttpClientHelpers
{
    // See https://github.com/open-telemetry/opentelemetry-proto/pull/781
    internal const int DefaultMessageSizeLimit = 4 * 1024 * 1024; // 4MiB

    /// <summary>
    /// Reads up to <paramref name="limit"/> bytes of a response body as a string,
    /// marking it as truncated if there was more.
    /// </summary>
    /// <param name="httpResponse">The response to read the body of.</param>
    /// <param name="limit">The maximum number of bytes to read.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use.</param>
    /// <returns>
    /// The response body, or <see langword="null"/> if there is no body or it could not be
    /// read before <paramref name="cancellationToken"/> was cancelled.
    /// </returns>
    internal static string? TryGetResponseBodyAsString(HttpResponseMessage? httpResponse, int limit, CancellationToken cancellationToken)
    {
        if (httpResponse?.Content is null || cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        try
        {
#if NET
            using var stream = httpResponse.Content.ReadAsStream(cancellationToken);
#else
            using var stream = httpResponse.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
#endif

            // A synchronous read cannot be cancelled once it is waiting for data, so a server that stops
            // sending part-way through the body would block it for as long as it keeps the connection open.
            // Not all streams observe the token once a read is in progress either (e.g. on .NET Framework,
            // or a custom HttpMessageHandler), so each read is raced against the token being cancelled.
            using var cancelled = cancellationToken.CanBeCanceled ? new CancellationWaiter(cancellationToken) : null;

            var length = GetBufferLength(stream, limit, httpResponse.Content.Headers.ContentLength);

#if NET
            var buffer = ArrayPool<byte>.Shared.Rent(length);
#else
            var buffer = new byte[length];
#endif

            try
            {
                var totalRead = 0;

                // Read raw bytes so the size limit applies to bytes rather than characters
                while (totalRead < length && !cancellationToken.IsCancellationRequested)
                {
                    var bytesRead = Read(stream, buffer, totalRead, length - totalRead, cancelled, cancellationToken);

                    if (bytesRead is 0)
                    {
                        break;
                    }

                    totalRead += bytesRead;
                }

                bool extra = false;

                if (totalRead == length)
                {
                    // We've read exactly length bytes. Check if there's more data.
                    var probe = new byte[1];

                    extra = Read(stream, probe, 0, 1, cancelled, cancellationToken) > 0;
                }

                // Decode using the charset from the response content headers, if available
                var encoding = GetEncoding(httpResponse.Content.Headers.ContentType?.CharSet);
                var result = encoding.GetString(buffer, 0, totalRead);

                if (extra)
                {
                    result += "[TRUNCATED]";
                }

                return result;
            }
            finally
            {
#if NET
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
#endif
            }
        }
        catch (Exception)
        {
            // The body is only used for diagnostics, so it is not worth failing over.
            return null;
        }
    }

    private static int Read(Stream stream, byte[] buffer, int offset, int count, CancellationWaiter? cancelled, CancellationToken cancellationToken)
    {
        if (cancelled is null)
        {
            return stream.Read(buffer, offset, count);
        }

#if NET
        var read = stream.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
#else
        var read = stream.ReadAsync(buffer, offset, count, cancellationToken);
#endif

        if (!read.IsCompleted && Task.WhenAny(read, cancelled.Task).GetAwaiter().GetResult() != read)
        {
            // Stop waiting for the read. It ends when the caller disposes of the stream,
            // so observe its failure then rather than leaving it unobserved.
            _ = read.ContinueWith(static t => _ = t.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            throw new OperationCanceledException(cancellationToken);
        }

        return read.GetAwaiter().GetResult();
    }

    private static int GetBufferLength(Stream stream, int limit, long? contentLength)
    {
        if (contentLength is { } value && value <= limit)
        {
            return (int)value;
        }

        try
        {
            // Avoid allocating an overly large buffer if the stream is smaller than the size limit
            return stream.Length < limit ? (int)stream.Length : limit;
        }
        catch (Exception)
        {
            // Not all Stream types support Length, so default to the maximum
            return limit;
        }
    }

    private static Encoding GetEncoding(string? name)
    {
        Encoding encoding = Encoding.UTF8;

        if (!string.IsNullOrWhiteSpace(name))
        {
            try
            {
                encoding = Encoding.GetEncoding(name);
            }
            catch (Exception)
            {
                // Invalid encoding name
            }
        }

        return encoding;
    }

    /// <summary>
    /// A task that completes when a <see cref="CancellationToken"/> is cancelled, created
    /// once per response so that each read of its body does not need to register for it.
    /// </summary>
    private sealed class CancellationWaiter : IDisposable
    {
        private readonly TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenRegistration registration;

        public CancellationWaiter(CancellationToken cancellationToken)
        {
            this.registration = cancellationToken.Register(
                static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true),
                this.completion);
        }

        public Task Task => this.completion.Task;

        public void Dispose() => this.registration.Dispose();
    }
}
