// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Rendering;

/// <summary>
/// Marks a stable layer subtree whose raster result may be cached by a backend.
/// </summary>
public sealed class RetainedLayer : RenderLayer {
    public RetainedLayer(Rect bounds, RenderLayer child) {
        ArgumentNullException.ThrowIfNull(child);
        Bounds = bounds;
        Child = child;
        CommandCount = child.CountCommands();
    }

    public Rect Bounds { get; }

    public RenderLayer Child { get; }

    public int CommandCount { get; }

    internal override void AppendCommands(List<RenderCommand> commands) {
        Child.AppendCommands(commands);
    }

    public override int CountCommands() {
        return CommandCount;
    }
}
