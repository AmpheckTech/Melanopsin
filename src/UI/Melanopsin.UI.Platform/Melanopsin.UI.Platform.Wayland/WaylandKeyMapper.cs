using Melanopsin.UI.Input;

namespace Melanopsin.UI.Platform.Wayland;

/// <summary>
/// Maps XKB keysyms to platform-neutral input keys.
/// </summary>
public static class WaylandKeyMapper {
    public static Key MapEvdevKeyCode(uint keycode) {
        return keycode switch {
            >= 59 and <= 68 => (Key)((int)Key.F1 + (int)keycode - 59),
            87 => Key.F11,
            88 => Key.F12,
            >= 183 and <= 194 =>
                (Key)((int)Key.F13 + (int)keycode - 183),
            _ => Key.Unknown,
        };
    }

    public static Key MapKeySym(uint keysym) {
        return keysym switch {
            >= 0x41 and <= 0x5A => (Key)((int)Key.A + (int)keysym - 0x41),
            >= 0x61 and <= 0x7A => (Key)((int)Key.A + (int)keysym - 0x61),
            >= 0x30 and <= 0x39 => (Key)((int)Key.D0 + (int)keysym - 0x30),
            >= 0xFFBE and <= 0xFFD5 =>
                (Key)((int)Key.F1 + (int)keysym - 0xFFBE),
            0xFF08 => Key.Backspace,
            0xFF09 => Key.Tab,
            0xFF0D or 0xFF8D => Key.Enter,
            0xFF13 => Key.Pause,
            0xFF14 => Key.ScrollLock,
            0xFF1B => Key.Escape,
            0xFF50 => Key.Home,
            0xFF51 => Key.Left,
            0xFF52 => Key.Up,
            0xFF53 => Key.Right,
            0xFF54 => Key.Down,
            0xFF55 => Key.PageUp,
            0xFF56 => Key.PageDown,
            0xFF57 => Key.End,
            0xFF61 => Key.PrintScreen,
            0xFF63 => Key.Insert,
            0xFF7F => Key.NumLock,
            0xFF8A => Key.NumPadMultiply,
            0xFF8B => Key.NumPadAdd,
            0xFF8C => Key.NumPadSeparator,
            0xFF8E => Key.NumPadDecimal,
            0xFF8F => Key.NumPadDivide,
            0xFF9F => Key.Delete,
            0xFFAA => Key.NumPadMultiply,
            0xFFAB => Key.NumPadAdd,
            0xFFAC => Key.NumPadSeparator,
            0xFFAD => Key.NumPadSubtract,
            0xFFAE => Key.NumPadDecimal,
            0xFFAF => Key.NumPadDivide,
            >= 0xFFB0 and <= 0xFFB9 =>
                (Key)((int)Key.NumPad0 + (int)keysym - 0xFFB0),
            0xFFE1 => Key.LeftShift,
            0xFFE2 => Key.RightShift,
            0xFFE3 => Key.LeftControl,
            0xFFE4 => Key.RightControl,
            0xFFE5 => Key.CapsLock,
            0xFFE7 => Key.LeftMeta,
            0xFFE8 => Key.RightMeta,
            0xFFE9 => Key.LeftAlt,
            0xFFEA => Key.RightAlt,
            0xFE03 => Key.RightAlt,
            0xFFEB => Key.LeftMeta,
            0xFFEC => Key.RightMeta,
            0xFF6A => Key.Help,
            0xFF67 => Key.Apps,
            0xFF6B => Key.Sleep,
            0x1008FF14 => Key.MediaPlayPause,
            0x1008FF15 => Key.MediaStop,
            0x1008FF16 => Key.MediaPreviousTrack,
            0x1008FF17 => Key.MediaNextTrack,
            0x1008FF12 => Key.VolumeMute,
            0x1008FF11 => Key.VolumeDown,
            0x1008FF13 => Key.VolumeUp,
            0x1008FF1B => Key.BrowserSearch,
            0x1008FF18 => Key.BrowserHome,
            0x1008FF26 => Key.BrowserBack,
            0x1008FF27 => Key.BrowserForward,
            0x1008FF29 => Key.BrowserRefresh,
            0x1008FF28 => Key.BrowserStop,
            0xFFFF => Key.Delete,
            0x0020 => Key.Space,
            0x003B or 0x003A => Key.Semicolon,
            0x003D or 0x002B => Key.Equal,
            0x002C or 0x003C => Key.Comma,
            0x002D or 0x005F => Key.Minus,
            0x002E or 0x003E => Key.Period,
            0x002F or 0x003F => Key.Slash,
            0x0060 or 0x007E => Key.Backquote,
            0x005B or 0x007B => Key.LeftBracket,
            0x005C or 0x007C => Key.Backslash,
            0x005D or 0x007D => Key.RightBracket,
            0x0027 or 0x0022 => Key.Quote,
            _ => Key.Unknown,
        };
    }
}
