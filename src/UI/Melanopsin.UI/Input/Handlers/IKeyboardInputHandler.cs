// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Input;

public interface IKeyboardInputHandler {
    bool HandleKey(KeyInputEvent input);
}
