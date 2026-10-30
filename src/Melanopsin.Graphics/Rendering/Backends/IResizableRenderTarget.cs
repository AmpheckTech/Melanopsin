namespace Melanopsin.Graphics.Rendering;

/// <summary>
/// Optional render-target capability for reusing backing storage across
/// resizes. Implementations must leave the target unchanged when returning
/// false.
/// </summary>
public interface IResizableRenderTarget : IRenderTarget {
    bool TryResize(RenderTargetDescriptor descriptor);
}
