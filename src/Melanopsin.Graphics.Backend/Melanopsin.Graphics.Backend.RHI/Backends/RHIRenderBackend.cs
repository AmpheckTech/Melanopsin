using Melanopsin.Graphics.Rendering;

namespace Melanopsin.Graphics.Backend.RHI;

public sealed class RHIRenderBackend : IRenderBackend {
    public string Name => "RHI";

    public void Dispose() {
    }

    public IRenderTarget CreateTarget(RenderTargetDescriptor descriptor) => new RHIRenderTarget(descriptor);
}
