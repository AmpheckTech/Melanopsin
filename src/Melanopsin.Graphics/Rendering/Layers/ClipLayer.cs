// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Rendering;

/// <summary>
/// Clips its child to a rectangle in the layer's parent coordinate space.
/// </summary>
public sealed class ClipLayer : RenderLayer {
    public ClipLayer(Rect bounds, RenderLayer child) {
        ArgumentNullException.ThrowIfNull(child);
        Bounds = bounds;
        Child = child;
    }

    public Rect Bounds { get; }

    public RenderLayer Child { get; }

    internal override void AppendCommands(List<RenderCommand> commands) {
        commands.Add(new PushClipCommand(Bounds));
        Child.AppendCommands(commands);
        commands.Add(new PopClipCommand());
    }

    public override int CountCommands() {
        return Child.CountCommands() + 2;
    }
}
