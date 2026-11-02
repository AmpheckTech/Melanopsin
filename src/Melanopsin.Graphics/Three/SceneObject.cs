namespace Melanopsin.Graphics.Three;

/// <summary>
/// A mesh with an object-to-world transform and optional color tint.
/// </summary>
public sealed class SceneObject {
    public SceneObject(Mesh mesh) {
        ArgumentNullException.ThrowIfNull(mesh);
        Mesh = mesh;
    }

    public Mesh Mesh { get; }

    public Matrix4x4 Transform { get; set; } = Matrix4x4.Identity;

    public Color? Tint { get; set; }
}
