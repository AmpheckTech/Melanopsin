using System.Runtime.InteropServices;
using Melanopsin.UI.Input;
using NWayland;
using NWayland.Protocols.Wayland;

namespace Melanopsin.UI.Platform.Wayland;

public sealed partial class WaylandPlatformHost {
    private void HandleKeyboardEnter(uint serial, WlSurface? surface) {
        _inputSerial = serial;
        var window = FindWindow(surface);
        _keyboardWindow = window;
        if (window is not null) {
            window.IsFocused = true;
            _events.QueueFocus(window, focused: true);
        }
    }

    private void HandleKeyboardLeave(uint serial, WlSurface? surface) {
        var window = FindWindow(surface);
        if (window is not null) {
            window.IsFocused = false;
            _events.QueueFocus(window, focused: false);
        }

        if (ReferenceEquals(_keyboardWindow, window)) {
            _keyboardWindow = null;
        }
    }

    private void HandleKeyboardKey(
        uint time,
        uint serial,
        uint key,
        WlKeyboard.KeyStateEnum state) {
        _inputSerial = serial;
        if (_keyboardWindow is not { } window) {
            return;
        }

        if (IsWindowInputBlocked(window)) {
            return;
        }

        var phase = state == WlKeyboard.KeyStateEnum.Released
            ? KeyPhase.Up
            : KeyPhase.Down;
        var keysym = _xkbState?.GetKeySymbol(key, phase) ?? 0;
        var text = phase == KeyPhase.Down
            ? _xkbState?.GetText(key)
            : null;
        var mappedKey = WaylandKeyMapper.MapKeySym(keysym);
        if (mappedKey == Key.Unknown) {
            mappedKey = WaylandKeyMapper.MapEvdevKeyCode(key);
        }

        var control = _xkbState?.IsModifierActive("Control") == true;
        var alt = _xkbState?.IsModifierActive("Mod1") == true;
        var meta = _xkbState?.IsModifierActive("Mod4") == true;
        if (phase == KeyPhase.Down &&
            !_preeditActive &&
            !control &&
            !alt &&
            !meta &&
            !string.IsNullOrEmpty(text)) {
            QueueKeyboardText(window, text);
        }

        _events.QueueKey(
            window,
            mappedKey,
            phase,
            _xkbState?.IsModifierActive("Shift") == true,
            control,
            alt,
            meta,
            text,
            time);
    }

    private void HandleKeyboardModifiers(
        uint depressed,
        uint latched,
        uint locked,
        uint group) {
        _xkbState?.UpdateModifiers(depressed, latched, locked, group);
    }

    private void HandleKeymap(int fd, uint size, bool valid) {
        _xkbState?.Dispose();
        _xkbState = null;
        if (!valid) {
            CloseFd(fd);
            return;
        }

        try {
            _xkbState = new WaylandXkbState(fd, size);
        } catch {
            // A broken keymap must not take down presentation; key-code
            // fallback still works without it.
            _xkbState = null;
        }
    }

    private void ReleaseKeyboard() {
        _keyboardWindow = null;
        if (_keyboard is null) {
            return;
        }

        if (_keyboard.IsReleaseAvailable) {
            _keyboard.Release();
        }

        _keyboard.Dispose();
        _keyboard = null;
    }

    private static void CloseFd(int fd) {
        if (fd >= 0) {
            _ = LibcClose(fd);
        }
    }

    [DllImport("libc", EntryPoint = "close")]
    private static extern int LibcClose(int fd);

    private sealed class KeyboardListener : WlKeyboard.Listener {
        private readonly WaylandPlatformHost _host;

        public KeyboardListener(WaylandPlatformHost host) {
            _host = host;
        }

        protected override void Keymap(
            WlKeyboard eventSender,
            WlKeyboard.KeymapFormatEnum format,
            WaylandFd fd,
            uint size) {
            var rawFd = fd.Consume();
            _host.HandleKeymap(
                rawFd,
                size,
                format == WlKeyboard.KeymapFormatEnum.XkbV1);
        }

        protected override void Enter(
            WlKeyboard eventSender,
            uint serial,
            WlSurface? surface,
            ReadOnlySpan<byte> keys) {
            _host.HandleKeyboardEnter(serial, surface);
        }

        protected override void Leave(
            WlKeyboard eventSender,
            uint serial,
            WlSurface? surface) {
            _host.HandleKeyboardLeave(serial, surface);
        }

        protected override void Key(
            WlKeyboard eventSender,
            uint serial,
            uint time,
            uint key,
            WlKeyboard.KeyStateEnum state) {
            _host.HandleKeyboardKey(time, serial, key, state);
        }

        protected override void Modifiers(
            WlKeyboard eventSender,
            uint serial,
            uint modsDepressed,
            uint modsLatched,
            uint modsLocked,
            uint group) {
            _host.HandleKeyboardModifiers(
                modsDepressed,
                modsLatched,
                modsLocked,
                group);
        }

        protected override void RepeatInfo(
            WlKeyboard eventSender,
            int rate,
            int delay) {
        }
    }
}
