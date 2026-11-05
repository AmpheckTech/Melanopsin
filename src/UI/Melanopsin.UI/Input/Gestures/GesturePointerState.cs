// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI.App;

namespace Melanopsin.UI.Input;

internal sealed class GesturePointerState {
    public GesturePointerState(
        PointerInputEvent input,
        WindowHitTarget? target,
        long timestamp) {
        Target = target;
        StartX = input.X;
        StartY = input.Y;
        LastX = input.X;
        LastY = input.Y;
        StartTimestamp = timestamp;
    }

    public WindowHitTarget? Target { get; }

    public float StartX { get; }

    public float StartY { get; }

    public float LastX { get; set; }

    public float LastY { get; set; }

    public long StartTimestamp { get; }

    public bool DragStarted { get; set; }

    public bool LongPressRaised { get; set; }
}
