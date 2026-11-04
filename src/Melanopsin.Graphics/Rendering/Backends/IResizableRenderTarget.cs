namespace Melanopsin.Graphics.Rendering;

/// <summary>
/// Optional render-target capability for reusing backing storage across
/// resizes.
/// </summary>
/// <remarks>
/// When <see cref="TryResize"/> returns false, the descriptor, dimensions, and
/// backing surface remain unchanged. Implementations must fail rather than
/// invalidate a surface while an outstanding frame lease still references it.
/// </remarks>
public interface IResizableRenderTarget : IRenderTarget {
    bool TryResize(RenderTargetDescriptor descriptor);
}
