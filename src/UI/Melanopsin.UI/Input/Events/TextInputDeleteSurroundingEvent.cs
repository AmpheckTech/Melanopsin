// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Input;

public sealed class TextInputDeleteSurroundingEvent {
    public TextInputDeleteSurroundingEvent(
        WindowId windowId,
        int beforeUtf8Bytes,
        int afterUtf8Bytes) {
        ArgumentOutOfRangeException.ThrowIfNegative(beforeUtf8Bytes);
        ArgumentOutOfRangeException.ThrowIfNegative(afterUtf8Bytes);
        WindowId = windowId;
        BeforeUtf8Bytes = beforeUtf8Bytes;
        AfterUtf8Bytes = afterUtf8Bytes;
    }

    public WindowId WindowId { get; }

    public int BeforeUtf8Bytes { get; }

    public int AfterUtf8Bytes { get; }
}
