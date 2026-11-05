// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Rendering;

/// <summary>
/// Converts a balanced command stream into an explicit layer tree.
/// </summary>
public static class RenderLayerBuilder {
    public static ContainerLayer Build(
        IReadOnlyList<RenderCommand> commands) {
        ArgumentNullException.ThrowIfNull(commands);
        var root = new RenderLayerContainerFrame(parent: null);
        RenderLayerFrame current = root;
        foreach (var command in commands) {
            switch (command) {
                case PushClipCommand clip:
                    current = new RenderLayerClipFrame(current, clip.Bounds);
                    break;
                case PopClipCommand:
                    current = CloseFrame<RenderLayerClipFrame>(current);
                    break;
                case PushTranslateCommand translate:
                    current = new RenderLayerTransformFrame(
                        current,
                        translate.X,
                        translate.Y);
                    break;
                case PopTranslateCommand:
                    current = CloseFrame<RenderLayerTransformFrame>(current);
                    break;
                default:
                    current.AddCommand(command);
                    break;
            }
        }

        if (!ReferenceEquals(current, root)) {
            throw new InvalidOperationException(
                "Render commands contain unbalanced layer operations.");
        }

        return (ContainerLayer)root.Build();
    }

    private static RenderLayerFrame CloseFrame<TFrame>(
        RenderLayerFrame current)
        where TFrame : RenderLayerFrame {
        if (current is not TFrame frame || frame.Parent is null) {
            throw new InvalidOperationException(
                $"Render commands contain an unmatched {typeof(TFrame).Name} close.");
        }

        frame.Parent.AddLayer(frame.Build());
        return frame.Parent;
    }

}
