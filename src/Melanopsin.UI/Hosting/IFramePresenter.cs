using Melanopsin.Graphics.Rendering;

namespace Melanopsin.UI.Hosting;

/// <summary>
/// Presents a completed render frame for a UI window to an output surface.
/// Native hosts can map this to a window, a compositor surface, or a capture.
/// </summary>
public interface IFramePresenter {
    void Present(WindowDescriptor window, RenderFrame frame);
}
