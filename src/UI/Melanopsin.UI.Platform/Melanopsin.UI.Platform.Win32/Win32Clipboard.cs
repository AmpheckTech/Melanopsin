// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Melanopsin.UI.Platform.Win32;

internal sealed class Win32Clipboard : IPlatformClipboard {
    public bool IsSupported => OperatingSystem.IsWindows();

    public ValueTask SetTextAsync(
        string text,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();
        OpenClipboard();
        try {
            if (!Win32Native.EmptyClipboard()) {
                throw LastError("EmptyClipboard");
            }

            var characters = text.ToCharArray();
            var byteCount = checked((characters.Length + 1) * sizeof(char));
            var memory = Win32Native.GlobalAlloc(
                Win32Native.GmemMoveable,
                (UIntPtr)(uint)byteCount);
            if (memory == IntPtr.Zero) {
                throw LastError("GlobalAlloc");
            }

            var pointer = Win32Native.GlobalLock(memory);
            if (pointer == IntPtr.Zero) {
                _ = Win32Native.GlobalFree(memory);
                throw LastError("GlobalLock");
            }

            try {
                Marshal.Copy(characters, 0, pointer, characters.Length);
                Marshal.WriteInt16(pointer, characters.Length * sizeof(char), 0);
            } finally {
                _ = Win32Native.GlobalUnlock(memory);
            }

            if (Win32Native.SetClipboardData(Win32Native.CfUnicodeText, memory) == IntPtr.Zero) {
                _ = Win32Native.GlobalFree(memory);
                throw LastError("SetClipboardData");
            }
        } finally {
            _ = Win32Native.CloseClipboard();
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<string?> GetTextAsync(
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        OpenClipboard();
        try {
            if (!Win32Native.IsClipboardFormatAvailable(Win32Native.CfUnicodeText)) {
                return ValueTask.FromResult<string?>(null);
            }

            var memory = Win32Native.GetClipboardData(Win32Native.CfUnicodeText);
            if (memory == IntPtr.Zero) {
                throw LastError("GetClipboardData");
            }

            var pointer = Win32Native.GlobalLock(memory);
            if (pointer == IntPtr.Zero) {
                throw LastError("GlobalLock");
            }

            try {
                return ValueTask.FromResult(Marshal.PtrToStringUni(pointer));
            } finally {
                _ = Win32Native.GlobalUnlock(memory);
            }
        } finally {
            _ = Win32Native.CloseClipboard();
        }
    }

    private static void OpenClipboard() {
        if (!Win32Native.OpenClipboard(IntPtr.Zero)) {
            throw LastError("OpenClipboard");
        }
    }

    private static Win32Exception LastError(string operation) {
        return new Win32Exception(
            Marshal.GetLastWin32Error(),
            $"Win32 clipboard operation '{operation}' failed.");
    }
}
