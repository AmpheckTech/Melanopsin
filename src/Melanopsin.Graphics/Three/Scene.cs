namespace Melanopsin.Graphics.Three;

/// <summary>
/// A lightweight three-dimensional scene exposed as an optional compositing
/// resource to normal two-dimensional UI.
/// </summary>
public sealed class Scene {
    private readonly List<SceneObject> _objects = [];

    public Scene(PerspectiveCamera? camera = null) {
        Camera = camera ?? new PerspectiveCamera(
            position: new(0, 0, 5),
            target: default);
    }

    public event Action? Invalidated;

    public PerspectiveCamera Camera { get; set; }

    public Color ClearColor { get; set; } = new(14, 20, 30);

    public IReadOnlyList<SceneObject> Objects => _objects;

    public void Add(SceneObject sceneObject) {
        ArgumentNullException.ThrowIfNull(sceneObject);
        _objects.Add(sceneObject);
        Invalidate();
    }

    public bool Remove(SceneObject sceneObject) {
        ArgumentNullException.ThrowIfNull(sceneObject);
        if (!_objects.Remove(sceneObject)) {
            return false;
        }

        Invalidate();
        return true;
    }

    public void Clear() {
        if (_objects.Count == 0) {
            return;
        }

        _objects.Clear();
        Invalidate();
    }

    /// <summary>
    /// Notifies observers after mutating a camera, object transform, material,
    /// or other scene value directly.
    /// </summary>
    public void Invalidate() {
        Invalidated?.Invoke();
    }
}
