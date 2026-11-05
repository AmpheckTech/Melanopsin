// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Hosting;

public enum WindowEventKind {
    CloseRequested,
    Resized,
    PointerInput,
    KeyInput,
    TextComposition,
    TextDeleteSurrounding,
    Shown,
    Activated,
    Deactivated,
    Moved,
    DpiChanged,
    RenderRequested,
    NotificationActivated,
}
