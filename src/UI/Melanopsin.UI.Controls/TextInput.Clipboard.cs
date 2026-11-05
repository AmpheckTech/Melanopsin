using Melanopsin.UI.App;

namespace Melanopsin.UI.Controls;

public sealed partial class TextInput {
    private long _textRevision;

    private void CopySelection() {
        var selectedText = SelectedText;
        if (selectedText.Length == 0 ||
            FindClipboard() is not { } clipboard) {
            return;
        }

        _ = WriteClipboardAsync(
            clipboard,
            selectedText,
            runtime: null,
            selectionStart: 0,
            selectionLength: 0,
            textRevision: 0,
            cut: false,
            MountToken);
    }

    private void CutSelection() {
        var selectedText = SelectedText;
        var runtime = FindComponentHost()?.Runtime;
        var clipboard = runtime?.Clipboard;
        if (IsReadOnly ||
            selectedText.Length == 0 ||
            runtime is null ||
            clipboard is not { IsSupported: true }) {
            return;
        }

        _ = WriteClipboardAsync(
            clipboard,
            selectedText,
            runtime,
            SelectionStart,
            SelectionLength,
            _textRevision,
            cut: true,
            MountToken);
    }

    private void PasteText() {
        if (IsReadOnly ||
            FindComponentHost()?.Runtime is not { } runtime ||
            runtime.Clipboard is not { IsSupported: true } clipboard) {
            return;
        }

        _ = ReadClipboardAsync(
            clipboard,
            runtime,
            SelectionStart,
            SelectionLength,
            _textRevision,
            MountToken);
    }

    private async Task WriteClipboardAsync(
        IPlatformClipboard clipboard,
        string text,
        UiRuntime? runtime,
        int selectionStart,
        int selectionLength,
        long textRevision,
        bool cut,
        CancellationToken cancellationToken) {
        try {
            await clipboard
                .SetTextAsync(text, cancellationToken)
                .ConfigureAwait(false);
        } catch (OperationCanceledException) {
            return;
        } catch {
            return;
        }

        if (!cut || runtime is null) {
            return;
        }

        DispatchToUi(runtime, () => {
            if (!CanApplyClipboardEdit(
                    textRevision,
                    selectionStart,
                    selectionLength)) {
                return;
            }

            _editing.ReplaceRange(selectionStart, selectionLength, string.Empty);
            OnTextEdited();
        });
    }

    private async Task ReadClipboardAsync(
        IPlatformClipboard clipboard,
        UiRuntime runtime,
        int selectionStart,
        int selectionLength,
        long textRevision,
        CancellationToken cancellationToken) {
        string? pastedText;
        try {
            pastedText = await clipboard
                .GetTextAsync(cancellationToken)
                .ConfigureAwait(false);
        } catch (OperationCanceledException) {
            return;
        } catch {
            return;
        }

        var insertableText = FilterControlCharacters(pastedText);
        if (insertableText.Length == 0) {
            return;
        }

        DispatchToUi(runtime, () => {
            if (!CanApplyClipboardEdit(
                    textRevision,
                    selectionStart,
                    selectionLength)) {
                return;
            }

            _editing.ReplaceRange(
                selectionStart,
                selectionLength,
                insertableText);
            OnTextEdited();
        });
    }

    private bool CanApplyClipboardEdit(
        long textRevision,
        int selectionStart,
        int selectionLength) {
        return IsMounted &&
               IsEnabled &&
               !IsReadOnly &&
               _textRevision == textRevision &&
               SelectionStart == selectionStart &&
               SelectionLength == selectionLength;
    }

    private void DispatchToUi(UiRuntime runtime, Action action) {
        if (runtime.Dispatcher.CheckAccess()) {
            action();
            return;
        }

        _ = runtime.Dispatcher.TryPost(action);
    }

    private IPlatformClipboard? FindClipboard() {
        return FindComponentHost()?.Runtime?.Clipboard is
            { IsSupported: true } clipboard
            ? clipboard
            : null;
    }

    private ComponentHost? FindComponentHost() {
        for (var node = Parent; node is not null; node = node.Parent) {
            if (node is ComponentHost host) {
                return host;
            }
        }

        return null;
    }
}
