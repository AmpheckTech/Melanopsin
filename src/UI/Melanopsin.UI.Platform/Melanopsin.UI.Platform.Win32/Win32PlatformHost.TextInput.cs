using Melanopsin.UI.Input;

namespace Melanopsin.UI.Platform.Win32;

public sealed partial class Win32PlatformHost {
    private TextInputState? _textInputState;
    private WindowId? _imeWindowId;
    private bool _imeEnabled;

    public void SetTextInput(TextInputState? state) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var previous = _textInputState;
        if (state == previous) {
            return;
        }

        _textInputState = state;
        var editorChanged = previous is not { } oldState ||
            state is not { } newState ||
            oldState.WindowId != newState.WindowId ||
            oldState.EditorId != newState.EditorId;
        if (editorChanged) {
            DetachTextInput();
        }

        if (state is not { IsReadOnly: false } current ||
            !_windows.TryGet(current.WindowId, out var window)) {
            return;
        }

        if (!AttachTextInput(window)) {
            return;
        }

        PositionTextInput(window, current);
    }

    private bool AttachTextInput(Win32Window window) {
        if (_imeEnabled && _imeWindowId == window.Id) {
            return true;
        }

        if (!Win32Native.ImmAssociateContextEx(
                window.Handle,
                IntPtr.Zero,
                Win32Native.ImeAssociateContextExDefault)) {
            return false;
        }

        _imeEnabled = true;
        _imeWindowId = window.Id;
        return true;
    }

    private void PositionTextInput(
        Win32Window window,
        TextInputState state) {
        var context = Win32Native.ImmGetContext(window.Handle);
        if (context == IntPtr.Zero) {
            return;
        }

        try {
            var caret = state.CursorBounds;
            var x = ToNativeCoordinate(caret.MinX);
            var y = ToNativeCoordinate(caret.MaxY);
            var compositionForm = new Win32CompositionForm {
                Style = Win32Native.ImeCompositionFormPoint,
                CurrentPosition = new Point { X = x, Y = y },
            };
            _ = Win32Native.ImmSetCompositionWindow(context, ref compositionForm);

            var candidateForm = new Win32CandidateForm {
                Style = Win32Native.ImeCandidateFormPosition,
                CurrentPosition = new Point { X = x, Y = y },
            };
            _ = Win32Native.ImmSetCandidateWindow(context, ref candidateForm);
        } finally {
            _ = Win32Native.ImmReleaseContext(window.Handle, context);
        }
    }

    private void DetachTextInput() {
        if (_imeEnabled && _imeWindowId is { } windowId &&
            _windows.TryGet(windowId, out var window)) {
            var context = Win32Native.ImmGetContext(window.Handle);
            if (context != IntPtr.Zero) {
                try {
                    _ = Win32Native.ImmNotifyIME(
                        context,
                        Win32Native.ImeNotifyCompositionString,
                        Win32Native.ImeCancelComposition,
                        0);
                } finally {
                    _ = Win32Native.ImmReleaseContext(window.Handle, context);
                }
            }

            _ = Win32Native.ImmAssociateContext(
                window.Handle,
                IntPtr.Zero);
        }

        _imeEnabled = false;
        _imeWindowId = null;
    }

    private static int ToNativeCoordinate(float value) {
        if (!float.IsFinite(value)) {
            return 0;
        }

        return (int)Math.Clamp(
            MathF.Round(value),
            int.MinValue,
            int.MaxValue);
    }
}
