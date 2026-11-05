// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Input;

public interface ICompositionInputHandler {
    bool HandleComposition(TextCompositionEvent input);
}
