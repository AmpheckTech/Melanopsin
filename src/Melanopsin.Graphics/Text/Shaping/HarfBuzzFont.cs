// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using HarfBuzzSharp;
using Melanopsin.Graphics.Text.Fonts;

namespace Melanopsin.Graphics.Text;

internal sealed class HarfBuzzFont : IDisposable {
    private readonly Blob _blob;

    public HarfBuzzFont(FontFace fontFace) {
        _blob = CreateBlob(fontFace);
        Face = new Face(_blob, fontFace.FaceIndex);
        Font = new HarfBuzzSharp.Font(Face);
        Font.SetScale(Face.UnitsPerEm, Face.UnitsPerEm);
    }

    public Face Face { get; }

    public HarfBuzzSharp.Font Font { get; }

    public void Dispose() {
        Exception? failure = null;
        Capture(ref failure, Font.Dispose);
        Capture(ref failure, Face.Dispose);
        Capture(ref failure, _blob.Dispose);
        if (failure is not null) {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static Blob CreateBlob(FontFace fontFace) {
        if (!string.IsNullOrWhiteSpace(fontFace.SourceName) &&
            File.Exists(fontFace.SourceName)) {
            return Blob.FromFile(fontFace.SourceName);
        }

        if (MemoryMarshal.TryGetArray(
                fontFace.Data,
                out var segment) &&
            segment.Array is not null) {
            var handle = GCHandle.Alloc(
                segment.Array,
                GCHandleType.Pinned);
            try {
                var pointer = IntPtr.Add(
                    handle.AddrOfPinnedObject(),
                    segment.Offset);
                return new Blob(
                    pointer,
                    segment.Count,
                    MemoryMode.Duplicate);
            } finally {
                handle.Free();
            }
        }

        var bytes = fontFace.Data.ToArray();
        var fallbackHandle = GCHandle.Alloc(
            bytes,
            GCHandleType.Pinned);
        try {
            return new Blob(
                fallbackHandle.AddrOfPinnedObject(),
                bytes.Length,
                MemoryMode.Duplicate);
        } finally {
            fallbackHandle.Free();
        }
    }

    private static void Capture(
        ref Exception? failure,
        Action action) {
        try {
            action();
        } catch (Exception exception) {
            failure = failure is null
                ? exception
                : failure is AggregateException aggregate
                    ? new AggregateException(
                        aggregate.InnerExceptions.Append(exception))
                    : new AggregateException(failure, exception);
        }
    }
}
