// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Input;

public interface ITextInputDeleteHandler {
    bool DeleteSurroundingText(TextInputDeleteSurroundingEvent input);
}
