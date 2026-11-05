using System.Text;
using Melanopsin.UI.Input;
using NWayland.Protocols.TextInputUnstableV3;
using NWayland.Protocols.Wayland;

namespace Melanopsin.UI.Platform.Wayland;

public sealed partial class WaylandPlatformHost {
    private ZwpTextInputV3? _textInput;
    private TextInputListener? _textInputListener;
    private TextInputState? _textInputState;
    private WaylandWindow? _textInputWindow;
    private WlSurface? _textInputSurface;
    private bool _textInputEnabled;
    private string _pendingPreedit = string.Empty;
    private string _pendingCommit = string.Empty;
    private readonly List<PendingKeyboardText> _pendingKeyboardText = [];
    private bool _pendingPreeditSet;
    private bool _preeditActive;
    private uint _pendingDeleteBefore;
    private uint _pendingDeleteAfter;

    public void SetTextInput(TextInputState? state) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var previous = _textInputState;
        _textInputState = state;
        EnsureTextInput();
        if (_textInput is null) {
            return;
        }

        if (state is null ||
            _textInputWindow is null ||
            _textInputWindow.Id != state.Value.WindowId) {
            DisableTextInput();
            return;
        }

        if (previous is { } oldState &&
            oldState.EditorId != state.Value.EditorId) {
            DisableTextInput();
        }

        ApplyTextInputState(state.Value);
    }

    private void EnsureTextInput() {
        if (_textInput is not null ||
            _connection.TextInputManager is not { } manager ||
            _connection.Seat is not { } seat) {
            return;
        }

        _textInputListener = new TextInputListener(this);
        _textInput = manager.GetTextInput(
            seat,
            _textInputListener,
            _connection.Queue);
    }

    private void HandleTextInputEnter(WlSurface? surface) {
        _textInputSurface = surface;
        _textInputWindow = FindWindow(surface);
        ResetPendingTextInput();
        _preeditActive = false;
        if (_textInputWindow is { } window &&
            _textInputState is { } state &&
            state.WindowId == window.Id) {
            ApplyTextInputState(state);
        }
    }

    private void HandleTextInputLeave(WlSurface? surface) {
        if (!ReferenceEquals(surface, _textInputSurface)) {
            return;
        }

        CancelPendingComposition();
        _textInputSurface = null;
        _textInputWindow = null;
        DisableTextInput();
    }

    private void ApplyTextInputState(TextInputState state) {
        if (_textInput is not { } textInput ||
            _textInputWindow is null ||
            _textInputWindow.Id != state.WindowId) {
            return;
        }

        if (!_textInputEnabled) {
            textInput.Enable();
            _textInputEnabled = true;
        }

        var (hint, purpose) = MapContentType(state);
        textInput.SetContentType(hint, purpose);
        textInput.SetSurroundingText(
            state.Text,
            GetByteOffset(state.Text, state.CaretIndex),
            GetAnchorByteOffset(state));
        textInput.SetTextChangeCause(
            ZwpTextInputV3.ChangeCauseEnum.Other);
        textInput.SetCursorRectangle(
            (int)MathF.Round(state.CursorBounds.MinX),
            (int)MathF.Round(state.CursorBounds.MinY),
            Math.Max(1, (int)MathF.Round(state.CursorBounds.Width)),
            Math.Max(1, (int)MathF.Round(state.CursorBounds.Height)));
        textInput.Commit();
        _connection.FlushChecked();
    }

    private void DisableTextInput() {
        CancelPendingComposition();
        _preeditActive = false;
        if (_textInput is not { } textInput || !_textInputEnabled) {
            return;
        }

        textInput.Disable();
        textInput.Commit();
        _textInputEnabled = false;
        _connection.FlushChecked();
    }

    private void HandlePreeditString(string text) {
        _pendingPreedit = text;
        _pendingPreeditSet = true;
        if (text.Length != 0) {
            ClaimPendingKeyboardText();
        }
    }

    private void HandleCommitString(string text) {
        _pendingCommit = text;
        if (text.Length != 0) {
            ClaimPendingKeyboardText();
        }
    }

    private void HandleDeleteSurroundingText(uint before, uint after) {
        _pendingDeleteBefore = Math.Max(_pendingDeleteBefore, before);
        _pendingDeleteAfter = Math.Max(_pendingDeleteAfter, after);
        if (before != 0 || after != 0) {
            ClaimPendingKeyboardText();
        }
    }

    private void QueueKeyboardText(WaylandWindow window, string text) {
        if (!_textInputEnabled || !ReferenceEquals(_textInputWindow, window)) {
            _events.QueueTextInput(window, text);
            return;
        }

        _pendingKeyboardText.Add(new PendingKeyboardText(window, text));
    }

    private void ClaimPendingKeyboardText() {
        if (_pendingKeyboardText.Count != 0) {
            _pendingKeyboardText.RemoveAt(_pendingKeyboardText.Count - 1);
        }
    }

    private void FlushPendingKeyboardText() {
        foreach (var pending in _pendingKeyboardText) {
            _events.QueueTextInput(pending.Window, pending.Text);
        }

        _pendingKeyboardText.Clear();
    }

    private void HandleTextInputDone() {
        if (_textInputWindow is not { } window) {
            ResetPendingTextInput();
            return;
        }

        if (_pendingPreedit.Length != 0) {
            _events.QueueTextComposition(
                window,
                _preeditActive
                    ? TextCompositionPhase.Update
                    : TextCompositionPhase.Start,
                _pendingPreedit);
            _preeditActive = true;
        } else if (_pendingPreeditSet && _preeditActive) {
            _events.QueueTextComposition(
                window,
                TextCompositionPhase.Cancel,
                string.Empty);
            _preeditActive = false;
        }

        if (_pendingDeleteBefore != 0 || _pendingDeleteAfter != 0) {
            _events.QueueTextDeleteSurrounding(
                window,
                _pendingDeleteBefore,
                _pendingDeleteAfter);
        }

        if (_pendingCommit.Length != 0) {
            _events.QueueTextComposition(
                window,
                TextCompositionPhase.Commit,
                _pendingCommit);
            _preeditActive = false;
        }

        ResetPendingTextInput();
    }

    private void CancelPendingComposition() {
        if (_preeditActive && _textInputWindow is { } window) {
            _events.QueueTextComposition(
                window,
                TextCompositionPhase.Cancel,
                string.Empty);
        }

        ResetPendingTextInput();
    }

    private void ResetPendingTextInput() {
        _pendingPreedit = string.Empty;
        _pendingCommit = string.Empty;
        _pendingPreeditSet = false;
        _pendingDeleteBefore = 0;
        _pendingDeleteAfter = 0;
    }

    private readonly record struct PendingKeyboardText(
        WaylandWindow Window,
        string Text);

    private void ReleaseTextInput() {
        CancelPendingComposition();
        _textInputEnabled = false;
        _textInputState = null;
        _textInputWindow = null;
        _textInputSurface = null;
        _textInputListener = null;
        if (_textInput is null) {
            return;
        }

        if (!_textInput.IsDisposed) {
            _textInput.Destroy();
        }

        _textInput.Dispose();
        _textInput = null;
    }

    private static int GetAnchorByteOffset(TextInputState state) {
        var anchor = state.SelectionLength == 0
            ? state.CaretIndex
            : state.SelectionStart + state.SelectionLength == state.CaretIndex
                ? state.SelectionStart
                : state.SelectionStart + state.SelectionLength;
        return GetByteOffset(state.Text, anchor);
    }

    private static int GetByteOffset(string text, int utf16Offset) {
        var offset = Math.Clamp(utf16Offset, 0, text.Length);
        if (offset > 0 && offset < text.Length &&
            char.IsLowSurrogate(text[offset])) {
            offset--;
        }

        return Encoding.UTF8.GetByteCount(text.AsSpan(0, offset));
    }

    private static (ZwpTextInputV3.ContentHintEnum Hint,
        ZwpTextInputV3.ContentPurposeEnum Purpose) MapContentType(
        TextInputState state) {
        var hint = state.IsSensitive
            ? ZwpTextInputV3.ContentHintEnum.HiddenText |
                ZwpTextInputV3.ContentHintEnum.SensitiveData
            : ZwpTextInputV3.ContentHintEnum.Completion |
                ZwpTextInputV3.ContentHintEnum.Spellcheck;
        var purpose = state.Purpose switch {
            TextInputPurpose.Number => ZwpTextInputV3.ContentPurposeEnum.Number,
            TextInputPurpose.Phone => ZwpTextInputV3.ContentPurposeEnum.Phone,
            TextInputPurpose.Url => ZwpTextInputV3.ContentPurposeEnum.Url,
            TextInputPurpose.Email => ZwpTextInputV3.ContentPurposeEnum.Email,
            TextInputPurpose.Password => ZwpTextInputV3.ContentPurposeEnum.Password,
            TextInputPurpose.Pin => ZwpTextInputV3.ContentPurposeEnum.Pin,
            TextInputPurpose.Terminal => ZwpTextInputV3.ContentPurposeEnum.Terminal,
            _ => ZwpTextInputV3.ContentPurposeEnum.Normal,
        };
        return (hint, purpose);
    }

    private sealed class TextInputListener : ZwpTextInputV3.Listener {
        private readonly WaylandPlatformHost _host;

        public TextInputListener(WaylandPlatformHost host) {
            _host = host;
        }

        protected override void Enter(
            ZwpTextInputV3 eventSender,
            WlSurface? surface) {
            _host.HandleTextInputEnter(surface);
        }

        protected override void Leave(
            ZwpTextInputV3 eventSender,
            WlSurface? surface) {
            _host.HandleTextInputLeave(surface);
        }

        protected override void PreeditString(
            ZwpTextInputV3 eventSender,
            string? text,
            int cursorBegin,
            int cursorEnd) {
            _host.HandlePreeditString(text ?? string.Empty);
        }

        protected override void CommitString(
            ZwpTextInputV3 eventSender,
            string? text) {
            _host.HandleCommitString(text ?? string.Empty);
        }

        protected override void DeleteSurroundingText(
            ZwpTextInputV3 eventSender,
            uint beforeLength,
            uint afterLength) {
            _host.HandleDeleteSurroundingText(beforeLength, afterLength);
        }

        protected override void Done(
            ZwpTextInputV3 eventSender,
            uint serial) {
            _host.HandleTextInputDone();
        }
    }
}
