// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Three;

/// <summary>
/// Portable software renderer for the optional 3D viewport. It stays behind the
/// texture boundary: callers receive an ordinary BGRA image.
/// </summary>
public sealed class SoftwareSceneRenderer {
    private static readonly Vector3 LightDirection =
        new Vector3(-0.45f, -0.7f, -0.55f).Normalize();
    private byte[] _pixels = [];
    private float[] _depth = [];

    public RenderedScene Render(Scene scene, PixelSize size) {
        ArgumentNullException.ThrowIfNull(scene);
        EnsureBuffers(size);
        Clear(scene.ClearColor);

        var camera = scene.Camera;
        var aspectRatio = (float)size.Width / size.Height;
        var view = camera.CreateViewMatrix();
        var projection = camera.CreateProjectionMatrix(aspectRatio);
        foreach (var sceneObject in scene.Objects) {
            DrawObject(sceneObject, view, projection, camera.NearPlane, size);
        }

        var stride = checked(size.Width * 4);
        var byteCount = checked(stride * size.Height);
        return new RenderedScene(
            size,
            stride,
            _pixels.AsMemory(0, byteCount));
    }

    private void DrawObject(
        SceneObject sceneObject,
        Matrix4x4 view,
        Matrix4x4 projection,
        float nearPlane,
        PixelSize size) {
        Span<ViewVertex> clipped = stackalloc ViewVertex[4];
        Span<ProjectedVertex> projected = stackalloc ProjectedVertex[4];
        foreach (var triangle in sceneObject.Mesh.Triangles) {
            var a = sceneObject.Transform.TransformPoint(triangle.A);
            var b = sceneObject.Transform.TransformPoint(triangle.B);
            var c = sceneObject.Transform.TransformPoint(triangle.C);
            var normal = Vector3.Cross(b - a, c - a);
            var color = ApplyLighting(
                ApplyTint(triangle.Color, sceneObject.Tint),
                normal);

            var viewA = view.TransformPoint(a);
            var viewB = view.TransformPoint(b);
            var viewC = view.TransformPoint(c);
            var clippedCount = ClipAgainstNearPlane(
                viewA,
                viewB,
                viewC,
                nearPlane,
                clipped);
            if (clippedCount < 3) {
                continue;
            }

            var valid = true;
            for (var index = 0; index < clippedCount; index++) {
                if (!TryProject(
                        clipped[index].Position,
                        projection,
                        size,
                        out projected[index])) {
                    valid = false;
                    break;
                }
            }

            if (!valid) {
                continue;
            }

            for (var index = 1; index < clippedCount - 1; index++) {
                Rasterize(projected[0], projected[index], projected[index + 1], color, size);
            }
        }
    }

    private void Rasterize(
        ProjectedVertex a,
        ProjectedVertex b,
        ProjectedVertex c,
        Color color,
        PixelSize size) {
        var minX = Math.Max(
            0,
            (int)MathF.Floor(MathF.Min(a.X, MathF.Min(b.X, c.X))));
        var maxX = Math.Min(
            size.Width - 1,
            (int)MathF.Ceiling(MathF.Max(a.X, MathF.Max(b.X, c.X))));
        var minY = Math.Max(
            0,
            (int)MathF.Floor(MathF.Min(a.Y, MathF.Min(b.Y, c.Y))));
        var maxY = Math.Min(
            size.Height - 1,
            (int)MathF.Ceiling(MathF.Max(a.Y, MathF.Max(b.Y, c.Y))));
        if (minX > maxX || minY > maxY) {
            return;
        }

        var area = Edge(a, b, c.X, c.Y);
        if (MathF.Abs(area) <= float.Epsilon) {
            return;
        }

        var inverseArea = 1 / area;
        for (var y = minY; y <= maxY; y++) {
            var sampleY = y + 0.5f;
            for (var x = minX; x <= maxX; x++) {
                var sampleX = x + 0.5f;
                var weightA = Edge(b, c, sampleX, sampleY) * inverseArea;
                var weightB = Edge(c, a, sampleX, sampleY) * inverseArea;
                var weightC = 1 - weightA - weightB;
                if (weightA < -0.0001f ||
                    weightB < -0.0001f ||
                    weightC < -0.0001f) {
                    continue;
                }

                var depth = (weightA * a.Depth) +
                    (weightB * b.Depth) +
                    (weightC * c.Depth);
                var pixelIndex = (y * size.Width) + x;
                if (!float.IsFinite(depth) || depth >= _depth[pixelIndex]) {
                    continue;
                }

                WritePixel(pixelIndex, color);
                if (color.Alpha == byte.MaxValue) {
                    _depth[pixelIndex] = depth;
                }
            }
        }
    }

    private static int ClipAgainstNearPlane(
        Vector3 a,
        Vector3 b,
        Vector3 c,
        float nearPlane,
        Span<ViewVertex> destination) {
        Span<ViewVertex> source = stackalloc ViewVertex[3] {
            CreateViewVertex(a, nearPlane),
            CreateViewVertex(b, nearPlane),
            CreateViewVertex(c, nearPlane),
        };
        var count = 0;
        for (var index = 0; index < source.Length; index++) {
            var current = source[index];
            var next = source[(index + 1) % source.Length];
            var currentInside = current.Distance >= 0;
            var nextInside = next.Distance >= 0;
            if (currentInside) {
                destination[count++] = current;
            }

            if (currentInside == nextInside) {
                continue;
            }

            var intersection = current.Distance /
                (current.Distance - next.Distance);
            destination[count++] = new ViewVertex(
                Vector3.Lerp(current.Position, next.Position, intersection),
                float.Lerp(current.Distance, next.Distance, intersection));
        }

        return count;
    }

    private static ViewVertex CreateViewVertex(
        Vector3 position,
        float nearPlane) {
        return new ViewVertex(position, -position.Z - nearPlane);
    }

    private static bool TryProject(
        Vector3 viewPosition,
        Matrix4x4 projection,
        PixelSize size,
        out ProjectedVertex projected) {
        var clip = projection.Transform(new Vector4(viewPosition, 1));
        if (!float.IsFinite(clip.W) || clip.W <= 0.00001f) {
            projected = default;
            return false;
        }

        var inverseW = 1 / clip.W;
        var x = ((clip.X * inverseW) * 0.5f + 0.5f) * size.Width;
        var y = (0.5f - (clip.Y * inverseW * 0.5f)) * size.Height;
        var depth = clip.Z * inverseW;
        if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(depth)) {
            projected = default;
            return false;
        }

        projected = new ProjectedVertex(x, y, depth);
        return true;
    }

    private void EnsureBuffers(PixelSize size) {
        var pixelCount = checked(size.Width * size.Height);
        var byteCount = checked(pixelCount * 4);
        if (_pixels.Length != byteCount) {
            _pixels = new byte[byteCount];
            _depth = new float[pixelCount];
        }
    }

    private void Clear(Color color) {
        Array.Fill(_depth, float.PositiveInfinity);
        var blue = Premultiply(color.Blue, color.Alpha);
        var green = Premultiply(color.Green, color.Alpha);
        var red = Premultiply(color.Red, color.Alpha);
        for (var offset = 0; offset < _pixels.Length; offset += 4) {
            _pixels[offset] = blue;
            _pixels[offset + 1] = green;
            _pixels[offset + 2] = red;
            _pixels[offset + 3] = color.Alpha;
        }
    }

    private void WritePixel(int pixelIndex, Color color) {
        var offset = pixelIndex * 4;
        if (color.Alpha == byte.MaxValue) {
            _pixels[offset] = color.Blue;
            _pixels[offset + 1] = color.Green;
            _pixels[offset + 2] = color.Red;
            _pixels[offset + 3] = byte.MaxValue;
            return;
        }

        var inverseAlpha = byte.MaxValue - color.Alpha;
        _pixels[offset] = (byte)(Premultiply(color.Blue, color.Alpha) +
            (_pixels[offset] * inverseAlpha / byte.MaxValue));
        _pixels[offset + 1] = (byte)(Premultiply(color.Green, color.Alpha) +
            (_pixels[offset + 1] * inverseAlpha / byte.MaxValue));
        _pixels[offset + 2] = (byte)(Premultiply(color.Red, color.Alpha) +
            (_pixels[offset + 2] * inverseAlpha / byte.MaxValue));
        _pixels[offset + 3] = (byte)(color.Alpha +
            (_pixels[offset + 3] * inverseAlpha / byte.MaxValue));
    }

    private static Color ApplyLighting(Color color, Vector3 normal) {
        if (normal.LengthSquared <= float.Epsilon) {
            return color;
        }

        var illumination = 0.35f +
            (0.65f * MathF.Abs(Vector3.Dot(normal.Normalize(), LightDirection)));
        return new Color(
            Scale(color.Red, illumination),
            Scale(color.Green, illumination),
            Scale(color.Blue, illumination),
            color.Alpha);
    }

    private static Color ApplyTint(Color color, Color? tint) {
        if (tint is not { } value) {
            return color;
        }

        return new Color(
            Multiply(color.Red, value.Red),
            Multiply(color.Green, value.Green),
            Multiply(color.Blue, value.Blue),
            Multiply(color.Alpha, value.Alpha));
    }

    private static float Edge(
        ProjectedVertex a,
        ProjectedVertex b,
        float x,
        float y) {
        return ((x - a.X) * (b.Y - a.Y)) - ((y - a.Y) * (b.X - a.X));
    }

    private static byte Multiply(byte value, byte scale) {
        return (byte)(value * scale / byte.MaxValue);
    }

    private static byte Premultiply(byte value, byte alpha) {
        return Multiply(value, alpha);
    }

    private static byte Scale(byte value, float scale) {
        return (byte)Math.Clamp(value * scale, 0, byte.MaxValue);
    }

    private readonly record struct ViewVertex(Vector3 Position, float Distance);

    private readonly record struct ProjectedVertex(float X, float Y, float Depth);
}
