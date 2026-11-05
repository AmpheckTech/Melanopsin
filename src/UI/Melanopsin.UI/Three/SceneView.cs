using Melanopsin.Graphics.Three;
using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Input;
using Melanopsin.UI.Markup;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.Three;

/// <summary>
/// Composites an optional three-dimensional scene into the normal UI tree.
/// The control contributes only a regular BGRA image, so layout, clipping,
/// z-order, and presentation remain entirely two-dimensional concerns.
/// </summary>
[MarkupElement]
[MarkupAttribute("orbit", MarkupValueKind.Boolean)]
public sealed class SceneView : Control,
    IRenderCommandProvider,
    ILayoutFallbackSizeProvider,
    IInputHandler {
    private readonly SoftwareSceneRenderer _renderer = new();
    private Scene _scene;
    private bool _orbitOnDrag = true;
    private bool _isOrbiting;
    private float _lastPointerX;
    private float _lastPointerY;

    public SceneView()
        : this(new Scene()) {
    }

    public SceneView(Scene scene)
        : base("SceneView") {
        ArgumentNullException.ThrowIfNull(scene);
        _scene = scene;
        ClipToBounds = true;
        IsRepaintBoundary = true;
        InputHandler = this;
        Accessibility.Role = AccessibilityRole.Image;
    }

    [MarkupAttribute("scene")]
    public Scene Scene {
        get => _scene;
        set {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(_scene, value)) {
                return;
            }

            if (IsMounted) {
                _scene.Invalidated -= OnSceneInvalidated;
            }

            _scene = value;
            if (IsMounted) {
                _scene.Invalidated += OnSceneInvalidated;
            }

            InvalidatePaint();
        }
    }

    public bool OrbitOnDrag {
        get => _orbitOnDrag;
        set {
            if (_orbitOnDrag == value) {
                return;
            }

            _orbitOnDrag = value;
            _isOrbiting = false;
        }
    }

    public void InvalidateScene() {
        InvalidatePaint();
    }

    public void AppendRenderCommands(
        RenderCommandContext context,
        List<RenderCommand> commands) {
        var width = Math.Max(1, (int)MathF.Round(context.Bounds.Width));
        var height = Math.Max(1, (int)MathF.Round(context.Bounds.Height));
        var rendered = _renderer.Render(
            Scene,
            new PixelSize(width, height));
        commands.Add(new DrawImageCommand(
            context.Bounds,
            rendered.Size,
            RenderPixelFormat.Bgra8888Premultiplied,
            rendered.Stride,
            rendered.Pixels,
            context.CornerRadius));
    }

    void IInputHandler.Handle(RoutedInputEvent inputEvent) {
        if (!IsEnabled || !OrbitOnDrag) {
            return;
        }

        var input = inputEvent.Input;
        if (!input.IsPrimaryButton) {
            return;
        }

        switch (input.Phase) {
            case PointerPhase.Down:
                _isOrbiting = true;
                _lastPointerX = input.X;
                _lastPointerY = input.Y;
                inputEvent.Handled = true;
                break;
            case PointerPhase.Move when _isOrbiting:
                var deltaX = input.X - _lastPointerX;
                var deltaY = input.Y - _lastPointerY;
                _lastPointerX = input.X;
                _lastPointerY = input.Y;
                Scene.Camera.Orbit(deltaX * 0.35f, deltaY * 0.35f);
                Scene.Invalidate();
                inputEvent.Handled = true;
                break;
            case PointerPhase.Up or PointerPhase.Cancel or PointerPhase.Leave:
                _isOrbiting = false;
                break;
        }
    }

    float ILayoutFallbackSizeProvider.MeasureFallbackHeight(
        LayoutMeasureContext context,
        LayoutConstraints constraints) {
        return context.Metrics.Space(60);
    }

    protected internal override void OnMounted() {
        base.OnMounted();
        Scene.Invalidated += OnSceneInvalidated;
    }

    protected internal override void OnUnmounted() {
        Scene.Invalidated -= OnSceneInvalidated;
        _isOrbiting = false;
        base.OnUnmounted();
    }

    protected override void OnSetAttribute(string name, object? value) {
        base.OnSetAttribute(name, value);
        switch (name) {
            case "scene" when value is Scene scene:
                Scene = scene;
                break;
            case "orbit":
                OrbitOnDrag = value is bool orbit && orbit;
                break;
        }
    }

    private void OnSceneInvalidated() {
        InvalidatePaint();
    }
}
