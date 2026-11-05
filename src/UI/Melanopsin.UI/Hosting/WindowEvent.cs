// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI.Input;

namespace Melanopsin.UI.Hosting;

public readonly record struct WindowEvent(
    WindowId WindowId,
    WindowEventKind Kind,
    PixelSize Size,
    PointerInputEvent? Pointer = null,
    KeyInputEvent? Key = null,
    TextCompositionEvent? Composition = null,
    TextInputDeleteSurroundingEvent? TextDeleteSurrounding = null,
    NotificationActivatedEventArgs? Notification = null,
    int X = 0,
    int Y = 0,
    float DisplayScale = 0);
