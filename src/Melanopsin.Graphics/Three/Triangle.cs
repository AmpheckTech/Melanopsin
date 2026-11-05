// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Three;

/// <summary>
/// A single colored triangle in object space.
/// </summary>
public readonly record struct Triangle {
    public Triangle(Vector3 a, Vector3 b, Vector3 c, Color color) {
        A = a;
        B = b;
        C = c;
        Color = color;
    }

    public Vector3 A { get; }

    public Vector3 B { get; }

    public Vector3 C { get; }

    public Color Color { get; }
}
