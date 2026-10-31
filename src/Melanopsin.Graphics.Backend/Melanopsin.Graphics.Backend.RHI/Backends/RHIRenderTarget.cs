using Melanopsin.Graphics.Rendering;
using Silk.NET.Maths;

namespace Melanopsin.Graphics.Backend.RHI;

internal sealed class RHIRenderTarget : IResizableRenderTarget {
    private byte[] _pixels;

    public RHIRenderTarget(RenderTargetDescriptor descriptor) {
        Descriptor = descriptor;
        _pixels = new byte[descriptor.Size.Width * descriptor.Size.Height * 4];
    }

    public RenderTargetDescriptor Descriptor { get; private set; }

    public RenderFrame Render(RenderScene scene) {
        int stride = Descriptor.Size.Width * 4;

        for (int i = 0; i < Descriptor.Size.Height; i++) {
            for (int j = 0; j < Descriptor.Size.Width; j++) {
                int index = i * stride + j * 4;

                var color = new Vector3D<float> {
                    X = j,
                    Y = i,
                    Z = 1145
                };
                color = 255 * Vector3D.Normalize(color);

                _pixels[index + 0] = (byte)color.X; // R
                _pixels[index + 1] = (byte)color.Y; // G
                _pixels[index + 2] = (byte)color.Z; // B
            }
        }

        return new RenderFrame(
            Descriptor,
            RenderPixelFormat.Bgra8888Premultiplied,
            stride,
            _pixels,
            scene.Damage,
            scene.Statistics
        );
    }

    public bool TryResize(RenderTargetDescriptor descriptor) {
        Descriptor = descriptor;
        _pixels = new byte[descriptor.Size.Width * descriptor.Size.Height * 4];
        return true;
    }

    public void Dispose() {
    }
}
