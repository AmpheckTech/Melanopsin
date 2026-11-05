// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Rendering;

public sealed class PushTranslateCommand : RenderCommand {
    public PushTranslateCommand(float x, float y) {
        X = x;
        Y = y;
    }

    public float X { get; }

    public float Y { get; }
}
