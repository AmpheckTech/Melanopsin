using System;
using System.Collections.Generic;

namespace Melanopsin.UI.Platform.Win32.Windowing;

internal sealed class Win32WindowRegistry {
    private readonly Dictionary<WindowId, Win32Window> _windows = [];

    public int Count => _windows.Count;

    public IEnumerable<Win32Window> Windows => _windows.Values;

    public void Add(Win32Window window) {
        _windows.Add(window.Id, window);
    }

    public bool TryGet(WindowId id, out Win32Window window) {
        return _windows.TryGetValue(id, out window!);
    }

    public bool TryGet(IntPtr handle, out Win32Window window) {
        foreach (var candidate in _windows.Values) {
            if (candidate.Handle == handle) {
                window = candidate;
                return true;
            }
        }

        window = null!;
        return false;
    }

    public bool Remove(WindowId id, out Win32Window window) {
        return _windows.Remove(id, out window!);
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
        }

        if (failures is not null) {
            throw new AggregateException(
                "One or more Win32 windows failed to close cleanly.",
                failures);
        }
    }
}
