// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;

namespace Melanopsin.UI.Platform.Win32.Windowing;

internal sealed class Win32WindowRegistry {
    private readonly Dictionary<WindowId, Win32Window> _windows = [];
    private readonly Dictionary<IntPtr, Win32Window> _windowsByHandle = [];

    public int Count => _windows.Count;

    public IEnumerable<Win32Window> Windows => _windows.Values;

    public void Add(Win32Window window) {
        _windows.Add(window.Id, window);
        _windowsByHandle.Add(window.Handle, window);
    }

    public bool TryGet(WindowId id, out Win32Window window) {
        return _windows.TryGetValue(id, out window!);
    }

    public bool TryGet(IntPtr handle, out Win32Window window) {
        return _windowsByHandle.TryGetValue(handle, out window!);
    }

    public bool Remove(WindowId id, out Win32Window window) {
        if (!_windows.Remove(id, out window!)) {
            return false;
        }

        _windowsByHandle.Remove(window.Handle);
        return true;
    }

    public void CloseAll() {
        List<Exception>? failures = null;
        try {
            foreach (var window in _windows.Values) {
                var handle = window.Handle;
                try {
                    window.Dispose();
                } catch (Exception exception) {
                    (failures ??= []).Add(exception);
                }

                try {
                    _ = Win32Native.DestroyWindow(handle);
                } catch (Exception exception) {
                    (failures ??= []).Add(exception);
                }
            }
        } finally {
            _windows.Clear();
            _windowsByHandle.Clear();
        }

        if (failures is not null) {
            throw new AggregateException(
                "One or more Win32 windows failed to close cleanly.",
                failures);
        }
    }
}
