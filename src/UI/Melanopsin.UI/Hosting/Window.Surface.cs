using Melanopsin.Graphics.Rendering;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.Hosting;

public sealed partial class Window {
    public void Resize(PixelSize size) {
        ThrowIfDisposed();
        if (size == Descriptor.Size) {
            return;
        }

        var previousDescriptor = Descriptor;
        var targetDescriptor = new RenderTargetDescriptor(size);
        if (_target is IResizableRenderTarget resizable &&
            resizable.TryResize(targetDescriptor)) {
            Descriptor = new WindowDescriptor(
                previousDescriptor.Id,
                previousDescriptor.Title,
                size,
                previousDescriptor.OwnerId,
                previousDescriptor.IsModal);
            _pendingDamage = new DamageRegionBuilder(size);
            InvalidateViewport(forceFullDamage: true);
            return;
        }

        var replacement = _targetFactory(targetDescriptor);
        var previous = _target;
        _target = replacement;
        // An accepted presenter lease can still reference the old surface.
        // Retire it and dispose only after that lease is released.
        _retiredTargets.Add(previous);
        Descriptor = new WindowDescriptor(
            previousDescriptor.Id,
            previousDescriptor.Title,
            size,
            previousDescriptor.OwnerId,
            previousDescriptor.IsModal);

        _pendingDamage = new DamageRegionBuilder(size);
        InvalidateViewport(forceFullDamage: true);
        DrainRetiredTargets();
    }

    public void SetDisplayScale(float scaleFactor) {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scaleFactor);
        if (Math.Abs(DisplayScale - scaleFactor) < 0.0001f) {
            return;
        }

        DisplayScale = scaleFactor;
        InvalidateLayout(forceFullDamage: true);
    }
}
