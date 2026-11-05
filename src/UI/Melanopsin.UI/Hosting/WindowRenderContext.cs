using Melanopsin.UI.Rendering;
using Melanopsin.UI.Styling;

namespace Melanopsin.UI.Hosting;

/// <summary>
/// Owns all mutable rendering state for one window. Style, layout and paint
/// caches must never be shared between windows.
/// </summary>
public sealed class WindowRenderContext {
    public WindowRenderContext(
        StyleEngine styles,
        ILayoutEngine layout,
        IRenderSceneComposer composer) {
        ArgumentNullException.ThrowIfNull(styles);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(composer);
        Styles = styles;
        Layout = layout;
        Composer = composer;
    }

    public StyleEngine Styles { get; }

    public ILayoutEngine Layout { get; }

    public IRenderSceneComposer Composer { get; }

    internal void UpdateMetrics(PixelSize size, float displayScale) {
        Styles.UpdateMetrics(new UiMetrics(
            displayScale,
            viewportWidth: size.Width,
            viewportHeight: size.Height));
    }

    internal int ActiveTransitionCount => Styles.ActiveTransitionCount;

    internal bool AdvanceAnimations(Node root, float elapsedSeconds) {
        return Styles.AdvanceAnimations(root, elapsedSeconds);
    }

    internal void Prune(IReadOnlySet<Node> liveNodes) {
        Styles.Prune(liveNodes);
    }
}
