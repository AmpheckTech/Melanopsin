// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Rendering;

/// <summary>
/// Blurs content already painted behind <see cref="Bounds"/>, then draws the
/// child above that filtered backdrop. The child itself is not blurred.
/// </summary>
public sealed class BackdropBlurLayer : RenderLayer {
    public BackdropBlurLayer(
        float radius,
        Rect bounds,
        float cornerRadius,
        RenderLayer child) {
        ArgumentOutOfRangeException.ThrowIfNegative(radius);
        if (!float.IsFinite(radius)) {
            throw new ArgumentOutOfRangeException(nameof(radius));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(cornerRadius);
        if (!float.IsFinite(cornerRadius)) {
            throw new ArgumentOutOfRangeException(nameof(cornerRadius));
        }

        ArgumentNullException.ThrowIfNull(child);
        Radius = radius;
        Bounds = bounds;
        CornerRadius = cornerRadius;
        Child = child;
    }

    /// <summary>The Gaussian standard deviation in layout pixels.</summary>
    public float Radius { get; }

    public Rect Bounds { get; }

    public float CornerRadius { get; }

    public RenderLayer Child { get; }

    internal override void AppendCommands(List<RenderCommand> commands) {
        Child.AppendCommands(commands);
    }

    public override int CountCommands() {
        return Child.CountCommands();
    }
}
