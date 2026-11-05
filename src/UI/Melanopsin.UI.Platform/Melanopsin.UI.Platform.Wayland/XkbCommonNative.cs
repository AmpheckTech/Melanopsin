using System.Runtime.InteropServices;
using System.Text;
using Melanopsin.UI.Input;
using Microsoft.Win32.SafeHandles;

namespace Melanopsin.UI.Platform.Wayland;

internal sealed unsafe class WaylandXkbState : IDisposable {
    private const int KeymapFormatTextV1 = 1;
    private const int StateModsEffective = 8;
    private const uint XkbKeyDown = 1;
    private const uint XkbKeyUp = 0;
    private const uint MaximumEvdevKeyCode = 0x2ff;
    private const uint MaximumKeymapBytes = 16 * 1024 * 1024;
    private const int MaximumTextBytes = 256;

    private IntPtr _context;
    private IntPtr _keymap;
    private IntPtr _state;
    private bool _disposed;

    public WaylandXkbState(int fd, uint size) {
        try {
            var text = ReadKeymap(fd, size);
            _context = XkbCommonNative.ContextNew(0);
            if (_context == IntPtr.Zero) {
                throw new InvalidOperationException(
                    "xkbcommon could not create a context.");
            }

            _keymap = XkbCommonNative.KeymapNewFromString(
                _context,
                text,
                KeymapFormatTextV1,
                0);
            if (_keymap == IntPtr.Zero) {
                throw new InvalidOperationException(
                    "xkbcommon could not parse the keyboard keymap.");
            }

            _state = XkbCommonNative.StateNew(_keymap);
            if (_state == IntPtr.Zero) {
                throw new InvalidOperationException(
                    "xkbcommon could not create a keyboard state.");
            }
        } catch {
            Dispose();
            throw;
        }
    }

    public void UpdateModifiers(
        uint depressed,
        uint latched,
        uint locked,
        uint group) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _ = XkbCommonNative.StateUpdateMask(
            _state,
            depressed,
            latched,
            locked,
            group,
            0,
            0);
    }

    public uint GetKeySymbol(uint keycode, KeyPhase phase) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (keycode > MaximumEvdevKeyCode) {
            return 0;
        }

        var nativeKeycode = keycode + 8;
        _ = XkbCommonNative.StateUpdateKey(
            _state,
            nativeKeycode,
            phase == KeyPhase.Down ? XkbKeyDown : XkbKeyUp);
        return XkbCommonNative.StateKeyGetOneSym(_state, nativeKeycode);
    }

    public string? GetText(uint keycode) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (keycode > MaximumEvdevKeyCode) {
            return null;
        }

        var nativeKeycode = keycode + 8;
        var length = XkbCommonNative.StateKeyGetUtf8(
            _state,
            nativeKeycode,
            null,
            0);
        if (length <= 0 || length > MaximumTextBytes) {
            return null;
        }

        var buffer = new byte[length + 1];
        fixed (byte* pointer = buffer) {
            var copied = XkbCommonNative.StateKeyGetUtf8(
                _state,
                nativeKeycode,
                pointer,
                (nuint)buffer.Length);
            if (copied <= 0 || copied > length) {
                return null;
            }

            return Encoding.UTF8.GetString(buffer, 0, copied);
        }
    }

    public bool IsModifierActive(string name) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return XkbCommonNative.StateModifierNameIsActive(
            _state,
            name,
            StateModsEffective) != 0;
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        if (_state != IntPtr.Zero) {
            XkbCommonNative.StateUnref(_state);
            _state = IntPtr.Zero;
        }

        if (_keymap != IntPtr.Zero) {
            XkbCommonNative.KeymapUnref(_keymap);
            _keymap = IntPtr.Zero;
        }

        if (_context != IntPtr.Zero) {
            XkbCommonNative.ContextUnref(_context);
            _context = IntPtr.Zero;
        }
    }

    private static string ReadKeymap(int fd, uint size) {
        using var handle = new SafeFileHandle((IntPtr)fd, ownsHandle: true);
        if (size == 0) {
            throw new InvalidOperationException(
                "The Wayland keyboard keymap is empty.");
        }

        if (size > MaximumKeymapBytes) {
            throw new InvalidOperationException(
                "The Wayland keyboard keymap exceeds the supported size limit.");
        }

        var bytes = new byte[checked((int)size)];
        var offset = 0;
        while (offset < bytes.Length) {
            var read = RandomAccess.Read(
                handle,
                bytes.AsSpan(offset),
                offset);
            if (read <= 0) {
                throw new EndOfStreamException(
                    "Wayland closed the keyboard keymap descriptor.");
            }

            offset += read;
        }

        var terminator = Array.IndexOf(bytes, (byte)0);
        var length = terminator >= 0 ? terminator : bytes.Length;
        return Encoding.UTF8.GetString(bytes, 0, length);
    }
}

internal static unsafe partial class XkbCommonNative {
    private const string LibraryName = "libxkbcommon.so.0";

    [LibraryImport(LibraryName, EntryPoint = "xkb_context_new")]
    internal static partial IntPtr ContextNew(uint flags);

    [LibraryImport(LibraryName, EntryPoint = "xkb_context_unref")]
    internal static partial void ContextUnref(IntPtr context);

    [LibraryImport(
        LibraryName,
        EntryPoint = "xkb_keymap_new_from_string",
        StringMarshalling = StringMarshalling.Utf8)]
    internal static partial IntPtr KeymapNewFromString(
        IntPtr context,
        string value,
        int format,
        uint flags);

    [LibraryImport(LibraryName, EntryPoint = "xkb_keymap_unref")]
    internal static partial void KeymapUnref(IntPtr keymap);

    [LibraryImport(LibraryName, EntryPoint = "xkb_state_new")]
    internal static partial IntPtr StateNew(IntPtr keymap);

    [LibraryImport(LibraryName, EntryPoint = "xkb_state_unref")]
    internal static partial void StateUnref(IntPtr state);

    [LibraryImport(LibraryName, EntryPoint = "xkb_state_update_mask")]
    internal static partial int StateUpdateMask(
        IntPtr state,
        uint depressed,
        uint latched,
        uint locked,
        uint depressedLayout,
        uint latchedLayout,
        uint lockedLayout);

    [LibraryImport(LibraryName, EntryPoint = "xkb_state_update_key")]
    internal static partial int StateUpdateKey(
        IntPtr state,
        uint keycode,
        uint direction);

    [LibraryImport(LibraryName, EntryPoint = "xkb_state_key_get_one_sym")]
    internal static partial uint StateKeyGetOneSym(IntPtr state, uint keycode);

    [LibraryImport(LibraryName, EntryPoint = "xkb_state_key_get_utf8")]
    internal static partial int StateKeyGetUtf8(
        IntPtr state,
        uint keycode,
        byte* buffer,
        nuint size);

    [LibraryImport(
        LibraryName,
        EntryPoint = "xkb_state_mod_name_is_active",
        StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int StateModifierNameIsActive(
        IntPtr state,
        string name,
        int component);
}
