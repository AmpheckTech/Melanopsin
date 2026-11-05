// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Melanopsin.UI.LanguageServer.Protocol;

internal sealed class JsonRpcConnection : IDisposable {
    private readonly Stream _input;
    private readonly Stream _output;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public JsonRpcConnection(Stream input, Stream output) {
        _input = input;
        _output = output;
    }

    public async Task<JsonElement?> ReadAsync(CancellationToken cancellationToken) {
        var contentLength = -1;
        while (true) {
            var line = await ReadHeaderLineAsync(cancellationToken);
            if (line is null) {
                return null;
            }

            if (line.Length == 0) {
                break;
            }

            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) {
                var value = line.Substring("Content-Length:".Length).Trim();
                if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out contentLength) ||
                    contentLength < 0) {
                    throw new InvalidDataException($"Invalid LSP Content-Length header '{value}'.");
                }
            }
        }

        if (contentLength < 0) {
            throw new InvalidDataException("LSP message is missing Content-Length.");
        }

        var buffer = ArrayPool<byte>.Shared.Rent(contentLength);
        try {
            var read = 0;
            while (read < contentLength) {
                var count = await _input.ReadAsync(
                    buffer.AsMemory(read, contentLength - read),
                    cancellationToken);
                if (count == 0) {
                    throw new EndOfStreamException("LSP message ended before Content-Length bytes were read.");
                }

                read += count;
            }

            using var document = JsonDocument.Parse(buffer.AsMemory(0, contentLength));
            return document.RootElement.Clone();
        } finally {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public async Task SendAsync(JsonObject message, CancellationToken cancellationToken) {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message);
        var header = Encoding.ASCII.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n");
        await _writeLock.WaitAsync(cancellationToken);
        try {
            await _output.WriteAsync(header, cancellationToken);
            await _output.WriteAsync(bytes, cancellationToken);
            await _output.FlushAsync(cancellationToken);
        } finally {
            _writeLock.Release();
        }
    }

    public void Dispose() {
        _writeLock.Dispose();
    }

    private async Task<string?> ReadHeaderLineAsync(CancellationToken cancellationToken) {
        var bytes = new List<byte>(64);
        var buffer = new byte[1];
        while (true) {
            var count = await _input.ReadAsync(buffer, cancellationToken);
            if (count == 0) {
                return bytes.Count == 0
                    ? null
                    : throw new EndOfStreamException("Unexpected end of LSP header.");
            }

            if (buffer[0] == (byte)'\n') {
                if (bytes.Count > 0 && bytes[^1] == (byte)'\r') {
                    bytes.RemoveAt(bytes.Count - 1);
                }

                return Encoding.ASCII.GetString(bytes.ToArray());
            }

            bytes.Add(buffer[0]);
            if (bytes.Count > 16 * 1024) {
                throw new InvalidDataException("LSP header exceeds 16 KiB.");
            }
        }
    }
}
