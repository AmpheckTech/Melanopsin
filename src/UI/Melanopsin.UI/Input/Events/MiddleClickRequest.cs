// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Input;

/// <summary>
/// Describes a completed middle-button click as it bubbles to ancestors.
/// </summary>
public sealed class MiddleClickRequest {
    internal MiddleClickRequest(
        Node source,
        PointerInputEvent pointer) {
        Source = source;
        CurrentTarget = source;
        WindowId = pointer.WindowId;
        X = pointer.X;
        Y = pointer.Y;
        Device = pointer.Device;
        Timestamp = pointer.Timestamp;
    }

    public Node Source { get; }

    public Node CurrentTarget { get; internal set; }

    public WindowId WindowId { get; }

    public float X { get; }

    public float Y { get; }

    public InputDeviceKind Device { get; }

    public long Timestamp { get; }

    /// <summary>
    /// Stops the click from bubbling to ancestors.
    /// </summary>
    public bool Handled { get; set; }
}
