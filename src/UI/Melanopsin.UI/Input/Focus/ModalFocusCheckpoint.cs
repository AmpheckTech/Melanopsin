// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Input;

internal sealed record ModalFocusCheckpoint(
    Overlay Modal,
    Node? PreviousFocus);
