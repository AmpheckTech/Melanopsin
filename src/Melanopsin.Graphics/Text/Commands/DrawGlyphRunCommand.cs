// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.Graphics;
using Melanopsin.Graphics.Rendering;

namespace Melanopsin.Graphics.Text;

public sealed class DrawGlyphRunCommand : RenderCommand {
    public DrawGlyphRunCommand(
        Rect bounds,
        TextRun run,
        float baseline,
        Color color,
        float originX = 0) {
        ArgumentNullException.ThrowIfNull(run);
        Bounds = bounds;
        Run = run;
        Baseline = baseline;
        Color = color;
        OriginX = originX;
    }

    public Rect Bounds { get; }

    public TextRun Run { get; }

    public float Baseline { get; }

    public Color Color { get; }

    public float OriginX { get; }
}
