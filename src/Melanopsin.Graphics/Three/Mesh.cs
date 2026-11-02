namespace Melanopsin.Graphics.Three;

/// <summary>
/// Immutable triangle geometry.
/// </summary>
public sealed class Mesh {
    private readonly Triangle[] _triangles;

    public Mesh(IEnumerable<Triangle> triangles) {
        ArgumentNullException.ThrowIfNull(triangles);
        _triangles = [.. triangles];
    }

    public IReadOnlyList<Triangle> Triangles => _triangles;

    /// <summary>
    /// Creates an axis-aligned box. Each pair of triangles uses one of the six
    /// supplied face colors, or a distinct default color when none are given.
    /// </summary>
    public static Mesh CreateBox(
        float width,
        float height,
        float depth,
        IReadOnlyList<Color>? faceColors = null) {
        ValidateDimension(width, nameof(width));
        ValidateDimension(height, nameof(height));
        ValidateDimension(depth, nameof(depth));
        if (faceColors is not null && faceColors.Count != 6) {
            throw new ArgumentException(
                "A box requires exactly six face colors.",
                nameof(faceColors));
        }

        var half = new Vector3(width, height, depth) / 2;
        Vector3[] vertices = [
            new(-half.X, -half.Y, half.Z),
            new(half.X, -half.Y, half.Z),
            new(half.X, half.Y, half.Z),
            new(-half.X, half.Y, half.Z),
            new(-half.X, -half.Y, -half.Z),
            new(half.X, -half.Y, -half.Z),
            new(half.X, half.Y, -half.Z),
            new(-half.X, half.Y, -half.Z),
        ];
        int[] indices = [
            0, 1, 2, 0, 2, 3,
            5, 4, 7, 5, 7, 6,
            4, 0, 3, 4, 3, 7,
            1, 5, 6, 1, 6, 2,
            3, 2, 6, 3, 6, 7,
            4, 5, 1, 4, 1, 0,
        ];
        Color[] defaults = [
            new(63, 177, 194),
            new(69, 111, 194),
            new(106, 190, 137),
            new(206, 160, 72),
            new(202, 100, 100),
            new(147, 112, 202),
        ];

        var triangles = new Triangle[indices.Length / 3];
        for (var triangleIndex = 0; triangleIndex < triangles.Length; triangleIndex++) {
            var source = triangleIndex * 3;
            triangles[triangleIndex] = new Triangle(
                vertices[indices[source]],
                vertices[indices[source + 1]],
                vertices[indices[source + 2]],
                (faceColors ?? defaults)[triangleIndex / 2]);
        }

        return new Mesh(triangles);
    }

    private static void ValidateDimension(float value, string name) {
        if (!float.IsFinite(value) || value <= 0) {
            throw new ArgumentOutOfRangeException(name);
        }
    }
}
